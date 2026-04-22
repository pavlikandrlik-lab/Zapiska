using FluentAssertions;
using Moq;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Services.Security;

/// <summary>
/// Ověřuje, že AuthorizationService cachuje snapshot per osobaId v rámci jedné instance
/// (= per-request díky Scoped registraci v DI). Builder musí být zavolán nejvýše jednou
/// pro každý jedinečný osobaId bez ohledu na počet volání HasPermissionAsync / BuildSnapshotAsync.
/// </summary>
public sealed class AuthorizationServiceCacheTests
{
    private static AuthorizationSnapshot MakeSnapshot(bool isSuperAdmin = false) =>
        new(
            IsSuperAdmin: isSuperAdmin,
            GlobalPermissions: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());

    // ---------------------------------------------------------------------------
    // BuildSnapshotAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task BuildSnapshotAsync_CalledTwiceWithSameOsobaId_InvokesBuilderOnce()
    {
        // Arrange
        const int osobaId = 1;
        var builderMock = new Mock<IAuthorizationSnapshotBuilder>();
        builderMock
            .Setup(b => b.BuildAsync(osobaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSnapshot());

        var service = new AuthorizationService(builderMock.Object);

        // Act
        var snap1 = await service.BuildSnapshotAsync(osobaId, CancellationToken.None);
        var snap2 = await service.BuildSnapshotAsync(osobaId, CancellationToken.None);

        // Assert
        snap1.Should().BeSameAs(snap2, "druhé volání musí vrátit identický (cached) objekt");
        builderMock.Verify(
            b => b.BuildAsync(osobaId, It.IsAny<CancellationToken>()),
            Times.Once,
            "builder musí být zavolán přesně jednou pro stejné osobaId");
    }

    [Fact]
    public async Task BuildSnapshotAsync_CalledForDifferentOsobaIds_InvokesBuilderForEach()
    {
        // Arrange
        var snap1 = MakeSnapshot(isSuperAdmin: false);
        var snap2 = MakeSnapshot(isSuperAdmin: true);

        var builderMock = new Mock<IAuthorizationSnapshotBuilder>();
        builderMock.Setup(b => b.BuildAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(snap1);
        builderMock.Setup(b => b.BuildAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(snap2);

        var service = new AuthorizationService(builderMock.Object);

        // Act
        var result1 = await service.BuildSnapshotAsync(1, CancellationToken.None);
        var result2 = await service.BuildSnapshotAsync(2, CancellationToken.None);

        // Assert
        result1.Should().BeSameAs(snap1);
        result2.Should().BeSameAs(snap2);
        builderMock.Verify(b => b.BuildAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        builderMock.Verify(b => b.BuildAsync(2, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // HasPermissionAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HasPermissionAsync_CalledFiveTimesForSameOsobaId_BuildsSnapshotOnce()
    {
        // Arrange
        const int osobaId = 7;
        const string permKey = "records.edit";
        var snapshot = new AuthorizationSnapshot(
            IsSuperAdmin: false,
            GlobalPermissions: new HashSet<string>([permKey], StringComparer.OrdinalIgnoreCase),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());

        var builderMock = new Mock<IAuthorizationSnapshotBuilder>();
        builderMock
            .Setup(b => b.BuildAsync(osobaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var service = new AuthorizationService(builderMock.Object);

        // Act
        var results = new bool[5];
        for (var i = 0; i < 5; i++)
        {
            results[i] = await service.HasPermissionAsync(osobaId, permKey, ct: CancellationToken.None);
        }

        // Assert
        results.Should().AllBeEquivalentTo(true, "snapshot obsahuje dané oprávnění");
        builderMock.Verify(
            b => b.BuildAsync(osobaId, It.IsAny<CancellationToken>()),
            Times.Once,
            "snapshot musí být postaven přesně jednou i pro 5 po sobě jdoucích HasPermissionAsync volání");
    }

    [Fact]
    public async Task HasPermissionAsync_CalledForDifferentOsobaIds_ProducesIndependentSnapshots()
    {
        // Arrange — osoba 10 má oprávnění, osoba 20 nemá
        const string permKey = "projects.create";

        var snapWithPerm = new AuthorizationSnapshot(
            IsSuperAdmin: false,
            GlobalPermissions: new HashSet<string>([permKey], StringComparer.OrdinalIgnoreCase),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());

        var snapWithoutPerm = new AuthorizationSnapshot(
            IsSuperAdmin: false,
            GlobalPermissions: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());

        var builderMock = new Mock<IAuthorizationSnapshotBuilder>();
        builderMock.Setup(b => b.BuildAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(snapWithPerm);
        builderMock.Setup(b => b.BuildAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(snapWithoutPerm);

        var service = new AuthorizationService(builderMock.Object);

        // Act
        var person10HasPerm = await service.HasPermissionAsync(10, permKey, ct: CancellationToken.None);
        var person20HasPerm = await service.HasPermissionAsync(20, permKey, ct: CancellationToken.None);

        // Assert
        person10HasPerm.Should().BeTrue("osoba 10 má oprávnění");
        person20HasPerm.Should().BeFalse("osoba 20 nemá oprávnění");
        builderMock.Verify(b => b.BuildAsync(10, It.IsAny<CancellationToken>()), Times.Once);
        builderMock.Verify(b => b.BuildAsync(20, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // RequirePermissionAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RequirePermissionAsync_CalledTwice_BuildsSnapshotOnce()
    {
        // Arrange
        const int osobaId = 3;
        const string permKey = "settings.view";
        var snapshot = new AuthorizationSnapshot(
            IsSuperAdmin: false,
            GlobalPermissions: new HashSet<string>([permKey], StringComparer.OrdinalIgnoreCase),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());

        var builderMock = new Mock<IAuthorizationSnapshotBuilder>();
        builderMock.Setup(b => b.BuildAsync(osobaId, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);

        var service = new AuthorizationService(builderMock.Object);

        // Act — dvě RequirePermission volání nesmí way dvakrát hit DB
        await service.RequirePermissionAsync(osobaId, permKey, ct: CancellationToken.None);
        await service.RequirePermissionAsync(osobaId, permKey, ct: CancellationToken.None);

        // Assert
        builderMock.Verify(
            b => b.BuildAsync(osobaId, It.IsAny<CancellationToken>()),
            Times.Once,
            "RequirePermissionAsync musí využít cache — builder se zavolá jen jednou");
    }
}
