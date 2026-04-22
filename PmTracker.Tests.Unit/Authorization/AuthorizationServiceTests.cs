using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class AuthorizationServiceTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task BuildSnapshotAsync_ShouldReturnEmptySnapshot_ForUnknownPerson()
    {
        await using var db = CreateInMemoryDb();
        var service = new AuthorizationService(db);

        var snapshot = await service.BuildSnapshotAsync(999, CancellationToken.None);

        snapshot.IsSuperAdmin.Should().BeFalse();
        snapshot.GlobalPermissions.Should().BeEmpty();
        snapshot.PerProjectPermissions.Should().BeEmpty();
        snapshot.PerSubsystemPermissions.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildSnapshotAsync_ShouldSetIsSuperAdmin_WhenInAuthzSuperadmins()
    {
        await using var db = CreateInMemoryDb();
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.AuthzSuperadmins.Add(new AuthzSuperadminEntity { OsobaId = 42 });
        await db.SaveChangesAsync();

        var service = new AuthorizationService(db);
        var snapshot = await service.BuildSnapshotAsync(42, CancellationToken.None);

        snapshot.IsSuperAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task BuildSnapshotAsync_ShouldLoadProjectPermissionsFromAssignment()
    {
        await using var db = CreateInMemoryDb();
        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "ADM_PROJ", Nazev = "Admin", IsActive = true, Scope = RoleScope.Project });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "records.edit", Nazev = "Edit", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 1, Kod = "ADM_PROJ", Nazev = "Admin", AuthzRoleId = 100 });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity { Id = 1, OsobaId = 42, ProjektId = 777, RoleId = 1, DatumPrirazeni = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new AuthorizationService(db);
        var snapshot = await service.BuildSnapshotAsync(42, CancellationToken.None);

        snapshot.PerProjectPermissions.Should().ContainKey(777);
        snapshot.PerProjectPermissions[777].Should().Contain("records.edit");
    }

    [Fact]
    public async Task HasPermissionAsync_ShouldReturnTrue_WhenSuperAdmin()
    {
        await using var db = CreateInMemoryDb();
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.AuthzSuperadmins.Add(new AuthzSuperadminEntity { OsobaId = 42 });
        await db.SaveChangesAsync();

        var service = new AuthorizationService(db);

        (await service.HasPermissionAsync(42, "anything")).Should().BeTrue();
    }

    [Fact]
    public async Task RequirePermissionAsync_ShouldThrowForbidden_WhenMissing()
    {
        await using var db = CreateInMemoryDb();
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        await db.SaveChangesAsync();

        var service = new AuthorizationService(db);

        await FluentActions
            .Invoking(() => service.RequirePermissionAsync(42, "records.edit", projektId: 777))
            .Should()
            .ThrowAsync<ForbiddenException>()
            .WithMessage("*records.edit*");
    }

    [Fact]
    public async Task RequirePermissionAsync_ShouldNotThrow_WhenGranted()
    {
        await using var db = CreateInMemoryDb();
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.AuthzSuperadmins.Add(new AuthzSuperadminEntity { OsobaId = 42 });
        await db.SaveChangesAsync();

        var service = new AuthorizationService(db);

        var act = async () => await service.RequirePermissionAsync(42, "anything");
        await act.Should().NotThrowAsync();
    }
}
