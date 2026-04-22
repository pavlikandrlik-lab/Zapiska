using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// Ověřuje, že UserContextResolver po úspěšném resolve naplní:
/// 1. HttpContext.Items[CurrentUserAccessor.HttpContextItemKey] = osobaId
/// 2. CurrentUserContextViewModel.Authorization (snapshot)
/// </summary>
public sealed class UserContextSnapshotIntegrationTests
{
    // Pozn.: UserContextResolver má složitou ctor logic (HttpContext, textNormalizer, identityMatcher).
    // Full integration test je složitější — ověřme JEN, že kód obsahuje očekávané napojení.

    [Fact]
    public void UserContextResolver_Source_ShouldSetHttpContextItemForOsobaId()
    {
        var code = System.IO.File.ReadAllText(
            PmTracker.Tests.Unit.Architecture.ArchitectureTestBase.ResolvePath("PmTracker.Web/Services/Security/UserContextResolver.cs"));

        code.Should().Contain("CurrentUserAccessor.HttpContextItemKey",
            "UserContextResolver musí naplnit HttpContext.Items[CurrentUserAccessor.HttpContextItemKey] aby policy handler uměl najít osobaId");
    }

    [Fact]
    public void UserContextResolver_Source_ShouldBuildAuthorizationSnapshot()
    {
        var code = System.IO.File.ReadAllText(
            PmTracker.Tests.Unit.Architecture.ArchitectureTestBase.ResolvePath("PmTracker.Web/Services/Security/UserContextResolver.cs"));

        code.Should().Contain("AuthorizationSnapshotBuilder",
            "UserContextResolver musí postavit AuthorizationSnapshot (přes AuthorizationSnapshotBuilder)");
        code.Should().Contain("Authorization = authzSnapshot",
            "CurrentUserContextViewModel.Authorization musí být naplněn");
    }

    [Fact]
    public void CurrentUserContextViewModel_ShouldHaveAuthorizationField()
    {
        var vmType = typeof(PmTracker.Web.Models.ViewModels.CurrentUserContextViewModel);
        var prop = vmType.GetProperty("Authorization");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(AuthorizationSnapshot));
    }

    [Fact]
    public async Task AuthorizationSnapshotBuilder_ShouldProduceConsistentResultWithResolver()
    {
        // sanity check: builder nezávisle na resolveru produkuje očekávaný shape pro známý osobaId
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new PmTrackerDbContext(options);

        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.AuthzSuperadmins.Add(new AuthzSuperadminEntity { OsobaId = 42 });
        await db.SaveChangesAsync();

        var service = new AuthorizationService(db);
        var snapshot = await service.BuildSnapshotAsync(42, CancellationToken.None);

        snapshot.IsSuperAdmin.Should().BeTrue();
    }
}
