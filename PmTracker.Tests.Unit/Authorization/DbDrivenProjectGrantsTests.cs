using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class DbDrivenProjectGrantsTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task LoadDbDrivenProjectRoleGrantsAsync_ShouldReturnSeededGrantsForProjectRole()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity
        {
            Id = 100, Kod = "ADM_PROJ", Nazev = "Projektový admin",
            IsActive = true, Scope = RoleScope.Project
        });
        db.AuthzPermissions.Add(new AuthzPermissionEntity
        {
            Id = 200, Klic = "records.edit", Nazev = "Editovat záznamy",
            CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project,
            IsActive = true, IsSystem = true
        });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity
        {
            Id = 300, RoleId = 100, PermissionId = 200,
            ScopeMode = ScopeMode.All, IsAllowed = true
        });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity
        {
            Id = 1, Kod = "ADM_PROJ", Nazev = "Projektový admin",
            AuthzRoleId = 100
        });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "Test", Prijmeni = "User" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            Id = 1, OsobaId = 42, ProjektId = 777, RoleId = 1,
            DatumPrirazeni = DateTime.UtcNow, DatumOdebrani = null
        });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().ContainSingle();
        var grant = grants[0];
        grant.PermissionKey.Should().Be("records.edit");
        grant.ScopeMode.Should().Be("INCLUDE");
        grant.IsAllowed.Should().BeTrue();
        grant.ProjectIds.Should().ContainSingle().Which.Should().Be(777);
    }

    [Fact]
    public async Task LoadDbDrivenProjectRoleGrantsAsync_ShouldAggregateMultipleProjectsPerPermission()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "ADM_PROJ", Nazev = "Admin", IsActive = true, Scope = RoleScope.Project });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "records.edit", Nazev = "Edit", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 1, Kod = "ADM_PROJ", Nazev = "Admin", AuthzRoleId = 100 });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniProjektu.AddRange(
            new ObsazeniProjektuEntity { Id = 10, OsobaId = 42, ProjektId = 777, RoleId = 1, DatumPrirazeni = DateTime.UtcNow },
            new ObsazeniProjektuEntity { Id = 11, OsobaId = 42, ProjektId = 888, RoleId = 1, DatumPrirazeni = DateTime.UtcNow });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().ContainSingle("stejný permission klíč má být sloučený do jednoho grantu s více ProjectIds");
        grants[0].ProjectIds.Should().BeEquivalentTo(new[] { 777, 888 });
    }

    [Fact]
    public async Task LoadDbDrivenProjectRoleGrantsAsync_ShouldSkipExpiredAssignments()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "ADM_PROJ", Nazev = "Admin", IsActive = true, Scope = RoleScope.Project });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "records.edit", Nazev = "Edit", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 1, Kod = "ADM_PROJ", Nazev = "Admin", AuthzRoleId = 100 });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            Id = 10, OsobaId = 42, ProjektId = 777, RoleId = 1,
            DatumPrirazeni = DateTime.UtcNow.AddDays(-30),
            DatumOdebrani = DateTime.UtcNow.AddDays(-1)
        });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);
        grants.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadDbDrivenProjectRoleGrantsAsync_ShouldSkipLookupRolesWithNullAuthzRoleId()
    {
        await using var db = CreateInMemoryDb();

        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 1, Kod = "CUSTOM_ROLE", Nazev = "Vlastní", AuthzRoleId = null });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity { Id = 10, OsobaId = 42, ProjektId = 777, RoleId = 1, DatumPrirazeni = DateTime.UtcNow });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);
        grants.Should().BeEmpty("custom lookup role bez AuthzRoleId nemůže přinést žádné granty");
    }

    [Fact]
    public async Task LoadDbDrivenProjectRoleGrantsAsync_ShouldSkipInactivePermissions()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "ADM_PROJ", Nazev = "A", IsActive = true, Scope = RoleScope.Project });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "deprecated.key", Nazev = "Old", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = false, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 1, Kod = "ADM_PROJ", Nazev = "A", AuthzRoleId = 100 });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity { Id = 10, OsobaId = 42, ProjektId = 777, RoleId = 1, DatumPrirazeni = DateTime.UtcNow });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);
        grants.Should().BeEmpty();
    }
}
