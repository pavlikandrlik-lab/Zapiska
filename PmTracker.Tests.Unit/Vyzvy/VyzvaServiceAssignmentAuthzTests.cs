using FluentAssertions;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Vyzvy;
using VyzvaUnit = PmTracker.Web.Services.Vyzvy.Unit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// TDD testy pro HIGH-2: ověřuje, že NastavitZaradidAsync a PrerditPnfAsync
/// kontrolují oprávnění records.edit na projektu vlastnícím ExterniOdkazId/CilovaVyzvaId.
/// </summary>
public sealed class VyzvaServiceAssignmentAuthzTests
{
    private const int OsobaId = 42;
    private const int ProjektId = 1;
    private const int CiziProjektId = 99;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Mock<IAuthorizationService> CreateAuthzMock(
        bool allowSourceProject = true, int? targetProjektId = null, bool allowTargetProject = true)
    {
        var mock = new Mock<IAuthorizationService>();

        mock.Setup(a => a.HasPermissionAsync(
                OsobaId,
                PermissionKeys.VyzvyPnfAssign,
                ProjektId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowSourceProject);

        if (targetProjektId.HasValue)
        {
            mock.Setup(a => a.HasPermissionAsync(
                    OsobaId,
                    PermissionKeys.VyzvyPnfAssign,
                    targetProjektId.Value,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(allowTargetProject);
        }

        return mock;
    }

    private static Mock<ICurrentUserAccessor> CreateUserMock(int? osobaId = OsobaId)
    {
        var mock = new Mock<ICurrentUserAccessor>();
        mock.Setup(u => u.OsobaId).Returns(osobaId);
        return mock;
    }

    /// <summary>Seeds a full PNF link with its owning projekt (projektId = 1).</summary>
    private static async Task<(PmTrackerDbContext db, int odkazId)> SeedPnfOdkazAsync(
        int odkazId = 10, int zaznamId = 1, int projektId = ProjektId, int? vyzvaId = null, bool zaradit = false)
    {
        var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db, projektId);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, zaznamId, projektId);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = odkazId,
            ZaznamId = zaznamId,
            TypOdkazuId = 1,
            Cislo = $"{odkazId}00000",
            VyzvaId = vyzvaId,
            ZaradidDoVyzvy = zaradit,
        });
        await db.SaveChangesAsync();
        return (db, odkazId);
    }

    // -------------------------------------------------------------------------
    // NastavitZaradidAsync — ACL checks
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NastavitZaradidAsync_WhenCallerLacksRecordsEditOnProject_ReturnsAccessDenied()
    {
        // Arrange
        var (db, odkazId) = await SeedPnfOdkazAsync();
        using (db)
        {
            var authz = CreateAuthzMock(allowSourceProject: false);
            var user = CreateUserMock();
            var svc = VyzvaServiceTestHarness.CreateService(db, authz: authz.Object, user: user.Object);

            // Act
            var result = await svc.NastavitZaradidAsync(odkazId, true, CancellationToken.None);

            // Assert
            result.Should().BeOfType<VyzvaResult<VyzvaUnit>.Fail>()
                .Which.Error.Code.Should().Be(VyzvaErrorCode.AccessDenied);
        }
    }

    [Fact]
    public async Task NastavitZaradidAsync_WhenCallerHasRecordsEditOnProject_ToggleSucceeds()
    {
        // Arrange
        var (db, odkazId) = await SeedPnfOdkazAsync();
        using (db)
        {
            var authz = CreateAuthzMock(allowSourceProject: true);
            var user = CreateUserMock();
            var svc = VyzvaServiceTestHarness.CreateService(db, authz: authz.Object, user: user.Object);

            // Act
            var result = await svc.NastavitZaradidAsync(odkazId, true, CancellationToken.None);

            // Assert
            result.Should().BeOfType<VyzvaResult<VyzvaUnit>.Ok>();
        }
    }

    [Fact]
    public async Task NastavitZaradidAsync_WhenOsobaIdIsNull_ReturnsAccessDenied()
    {
        // Arrange
        var (db, odkazId) = await SeedPnfOdkazAsync();
        using (db)
        {
            var authz = new Mock<IAuthorizationService>();
            var user = CreateUserMock(osobaId: null); // defense in depth: no user
            var svc = VyzvaServiceTestHarness.CreateService(db, authz: authz.Object, user: user.Object);

            // Act
            var result = await svc.NastavitZaradidAsync(odkazId, true, CancellationToken.None);

            // Assert
            result.Should().BeOfType<VyzvaResult<VyzvaUnit>.Fail>()
                .Which.Error.Code.Should().Be(VyzvaErrorCode.AccessDenied);

            // Authz must NOT be consulted when osobaId is null
            authz.Verify(a => a.HasPermissionAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    // -------------------------------------------------------------------------
    // PrerditPnfAsync — ACL checks
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PrerditPnfAsync_WhenCallerLacksPermissionOnSourceProject_ReturnsAccessDenied()
    {
        // Arrange
        var (db, odkazId) = await SeedPnfOdkazAsync();
        using (db)
        {
            var authz = CreateAuthzMock(allowSourceProject: false);
            var user = CreateUserMock();
            var svc = VyzvaServiceTestHarness.CreateService(db, authz: authz.Object, user: user.Object);

            // Act — přeřadit do null (odebrat z výzvy)
            var result = await svc.PrerditPnfAsync(odkazId, null, CancellationToken.None);

            // Assert
            result.Should().BeOfType<VyzvaResult<VyzvaUnit>.Fail>()
                .Which.Error.Code.Should().Be(VyzvaErrorCode.AccessDenied);
        }
    }

    [Fact]
    public async Task PrerditPnfAsync_WhenCrossProjectMove_RequiresPermissionOnBothProjects_DeniesIfMissingTarget()
    {
        // Arrange — source projekt = 1, target vyzva belongs to projekt = 99
        var (db, odkazId) = await SeedPnfOdkazAsync(odkazId: 20, zaznamId: 10, projektId: ProjektId);
        using (db)
        {
            // Seed target projekt + vyzva on CiziProjektId
            await VyzvaServiceTestHarness.SeedProjektAsync(db, CiziProjektId);
            db.Vyzvy.Add(new VyzvaEntity
            {
                Id = 500, ProjektId = CiziProjektId, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
                Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1,
                MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
            });
            await db.SaveChangesAsync();

            // User has records.edit on source (1) but NOT on target (99)
            var authz = new Mock<IAuthorizationService>();
            authz.Setup(a => a.HasPermissionAsync(OsobaId, PermissionKeys.VyzvyPnfReassign, ProjektId, null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
            authz.Setup(a => a.HasPermissionAsync(OsobaId, PermissionKeys.VyzvyPnfReassign, CiziProjektId, null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

            var user = CreateUserMock();
            var svc = VyzvaServiceTestHarness.CreateService(db, authz: authz.Object, user: user.Object);

            // Act
            var result = await svc.PrerditPnfAsync(odkazId, cilovaVyzvaId: 500, CancellationToken.None);

            // Assert
            result.Should().BeOfType<VyzvaResult<VyzvaUnit>.Fail>()
                .Which.Error.Code.Should().Be(VyzvaErrorCode.AccessDenied);
        }
    }

    [Fact]
    public async Task PrerditPnfAsync_WhenCrossProjectMove_AllowsIfPermissionOnBoth()
    {
        // Arrange — source projekt = 1, target vyzva belongs to projekt = 99
        var (db, odkazId) = await SeedPnfOdkazAsync(odkazId: 21, zaznamId: 11, projektId: ProjektId);
        using (db)
        {
            await VyzvaServiceTestHarness.SeedProjektAsync(db, CiziProjektId);
            db.Vyzvy.Add(new VyzvaEntity
            {
                Id = 501, ProjektId = CiziProjektId, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026,
                Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1,
                MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
            });
            await db.SaveChangesAsync();

            // User has records.edit on BOTH projects
            var authz = new Mock<IAuthorizationService>();
            authz.Setup(a => a.HasPermissionAsync(OsobaId, PermissionKeys.VyzvyPnfReassign, ProjektId, null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);
            authz.Setup(a => a.HasPermissionAsync(OsobaId, PermissionKeys.VyzvyPnfReassign, CiziProjektId, null, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

            var user = CreateUserMock();
            var svc = VyzvaServiceTestHarness.CreateService(db, authz: authz.Object, user: user.Object);

            // Act
            var result = await svc.PrerditPnfAsync(odkazId, cilovaVyzvaId: 501, CancellationToken.None);

            // Assert
            result.Should().BeOfType<VyzvaResult<VyzvaUnit>.Ok>();
        }
    }
}
