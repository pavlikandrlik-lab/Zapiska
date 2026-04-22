using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class SeedEndToEndTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task FullSeed_ShouldCreateAllSystemRolesWithScope()
    {
        await using var db = CreateInMemoryDb();
        var seeder = new PermissionSeeder(db);

        await seeder.SeedAsync(CancellationToken.None);

        var roles = await db.AuthzRoles.AsNoTracking().ToListAsync();
        roles.Select(r => r.Kod).Should().Contain(new[]
        {
            "SUPERADMIN", "APP_ADMIN",
            "VLASTNIK_PROJEKTU", "ADM_PROJ", "PROJ_MAN", "HOST", "GEST",
            "VEDOUCI_SUBSYSTEMU", "ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "METODIK_SUBSYSTEMU"
        });

        roles.First(r => r.Kod == "ADM_PROJ").Scope.Should().Be("PROJECT");
        roles.First(r => r.Kod == "VEDOUCI_SUBSYSTEMU").Scope.Should().Be("SUBSYSTEM");
        roles.First(r => r.Kod == "APP_ADMIN").Scope.Should().Be("GLOBAL");
    }

    [Fact]
    public async Task FullSeed_ShouldLinkLookupRolesToAuthzRoles()
    {
        await using var db = CreateInMemoryDb();

        // Arrange: existing lookup rows simulující stav po db_upgrade (prázdné AuthzRoleId).
        db.CiselnikRoliProjektu.AddRange(
            new CiselnikRoliProjektuEntity { Kod = "ADM_PROJ", Nazev = "Admin" },
            new CiselnikRoliProjektuEntity { Kod = "HOST", Nazev = "Host" });
        db.CiselnikRoliSubsystemu.Add(
            new CiselnikRoleSubsystemuEntity { Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí" });
        await db.SaveChangesAsync();

        var seeder = new PermissionSeeder(db);
        await seeder.SeedAsync(CancellationToken.None);

        var admProj = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "ADM_PROJ");
        admProj.AuthzRoleId.Should().NotBeNull();

        var host = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "HOST");
        host.AuthzRoleId.Should().NotBeNull();

        var vedouci = await db.CiselnikRoliSubsystemu.FirstAsync(r => r.Kod == "VEDOUCI_SUBSYSTEMU");
        vedouci.AuthzRoleId.Should().NotBeNull();
    }

    [Fact]
    public async Task FullSeed_ShouldCreateRolePermissionsForProjectRoles()
    {
        await using var db = CreateInMemoryDb();
        var seeder = new PermissionSeeder(db);

        await seeder.SeedAsync(CancellationToken.None);

        var admProjRole = await db.AuthzRoles.FirstAsync(r => r.Kod == "ADM_PROJ");
        var admProjPerms = await (
            from rp in db.AuthzRolePermissions.AsNoTracking()
            join p in db.AuthzPermissions.AsNoTracking() on rp.PermissionId equals p.Id
            where rp.RoleId == admProjRole.Id && rp.IsAllowed
            select p.Klic).ToListAsync();

        admProjPerms.Should().Contain(new[] { "records.edit", "meetings.edit", "team.manage" });
        admProjPerms.Should().NotContain("projects.edit"); // ADM_PROJ úmyslně bez projects.edit
    }
}
