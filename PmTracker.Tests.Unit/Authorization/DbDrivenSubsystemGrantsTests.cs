using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class DbDrivenSubsystemGrantsTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task LoadDbDrivenSubsystemRoleGrantsAsync_ShouldReturnSeededGrantsForSubsystemRole()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí", IsActive = true, Scope = RoleScope.Subsystem });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "meetings.notes.subsystemlead", Nazev = "Komentář", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity { Id = 1, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí", AuthzRoleId = 100 });
        db.ProjektSubsystemy.Add(new ProjektSubsystemEntity { Id = 50, ProjektId = 777, SubsystemId = 1, Poradi = 1 });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            Id = 1, OsobaId = 42, ProjektSubsystemId = 50, RoleSubsystemuId = 1,
            DatumPrirazeni = DateTime.UtcNow, DatumOdebrani = null
        });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenSubsystemRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().ContainSingle();
        grants[0].PermissionKey.Should().Be("meetings.notes.subsystemlead");
        grants[0].ScopeMode.Should().Be("INCLUDE");
        grants[0].ProjectIds.Should().ContainSingle().Which.Should().Be(777);
    }

    [Fact]
    public async Task LoadDbDrivenSubsystemRoleGrantsAsync_ShouldSkipExpiredAssignments()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "V", IsActive = true, Scope = RoleScope.Subsystem });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "meetings.notes.subsystemlead", Nazev = "K", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity { Id = 1, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "V", AuthzRoleId = 100 });
        db.ProjektSubsystemy.Add(new ProjektSubsystemEntity { Id = 50, ProjektId = 777, SubsystemId = 1, Poradi = 1 });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            Id = 1, OsobaId = 42, ProjektSubsystemId = 50, RoleSubsystemuId = 1,
            DatumPrirazeni = DateTime.UtcNow.AddDays(-30),
            DatumOdebrani = DateTime.UtcNow.AddDays(-1)
        });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenSubsystemRoleGrantsAsync(db, 42, CancellationToken.None);
        grants.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadDbDrivenSubsystemRoleGrantsAsync_ShouldSkipAssignmentsOnRemovedProjectSubsystem()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "V", IsActive = true, Scope = RoleScope.Subsystem });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "meetings.notes.subsystemlead", Nazev = "K", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity { Id = 1, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "V", AuthzRoleId = 100 });
        db.ProjektSubsystemy.Add(new ProjektSubsystemEntity
        {
            Id = 50, ProjektId = 777, SubsystemId = 1, Poradi = 1,
            DatumOdebrani = DateTime.UtcNow.AddDays(-5)
        });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            Id = 1, OsobaId = 42, ProjektSubsystemId = 50, RoleSubsystemuId = 1,
            DatumPrirazeni = DateTime.UtcNow, DatumOdebrani = null
        });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenSubsystemRoleGrantsAsync(db, 42, CancellationToken.None);
        grants.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadDbDrivenSubsystemRoleGrantsAsync_ShouldSkipWhenIsAllowedFalse()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "V", IsActive = true, Scope = RoleScope.Subsystem });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "meetings.notes.subsystemlead", Nazev = "K", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = false });  // denied
        db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity { Id = 1, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "V", AuthzRoleId = 100 });
        db.ProjektSubsystemy.Add(new ProjektSubsystemEntity { Id = 50, ProjektId = 777, SubsystemId = 1, Poradi = 1 });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity { Id = 1, OsobaId = 42, ProjektSubsystemId = 50, RoleSubsystemuId = 1, DatumPrirazeni = DateTime.UtcNow });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenSubsystemRoleGrantsAsync(db, 42, CancellationToken.None);
        grants.Should().BeEmpty("IsAllowed=false rows must be filtered out");
    }
}
