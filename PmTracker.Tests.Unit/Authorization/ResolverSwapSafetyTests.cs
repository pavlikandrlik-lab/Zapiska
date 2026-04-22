using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// Ověřuje, že DB-driven cesta (Task B1+B2) produkuje všechny granty, které
/// dříve dával builder (nic se neztratilo), a navíc přidává seed granty pro role,
/// které builder opomíjel (VLASTNIK_PROJEKTU, GEST, plný ADM_PROJ/PROJ_MAN matrix).
/// </summary>
public sealed class ResolverSwapSafetyTests
{
    private static PmTrackerDbContext CreateSeededDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new PmTrackerDbContext(options);

        // Lookup rows nejprve — linker volaný uvnitř SeedAsync propojí AuthzRoleId přes Kod
        db.CiselnikRoliProjektu.AddRange(
            new CiselnikRoliProjektuEntity { Kod = "VLASTNIK_PROJEKTU", Nazev = "Vlastník projektu" },
            new CiselnikRoliProjektuEntity { Kod = "ADM_PROJ", Nazev = "Projektový admin" },
            new CiselnikRoliProjektuEntity { Kod = "PROJ_MAN", Nazev = "Projektový manažer" },
            new CiselnikRoliProjektuEntity { Kod = "HOST", Nazev = "Host" },
            new CiselnikRoliProjektuEntity { Kod = "GEST", Nazev = "Gestor" });
        db.SaveChanges();

        var seeder = new PermissionSeeder(db);
        seeder.SeedAsync(CancellationToken.None).GetAwaiter().GetResult();

        return db;
    }

    [Fact]
    public async Task DbDrivenProjectGrants_ShouldCoverVLASTNIK_PROJEKTU()
    {
        await using var db = CreateSeededDb();
        var vlastnikRole = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "VLASTNIK_PROJEKTU");

        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "V", Prijmeni = "L" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            OsobaId = 42, ProjektId = 777, RoleId = vlastnikRole.Id,
            DatumPrirazeni = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        var grantKeys = grants.Select(g => g.PermissionKey).OrderBy(x => x).ToArray();
        // Fáze C — Task C2: přidány dashboard/export/comments klíče (celkem 14)
        grantKeys.Should().BeEquivalentTo(new[]
        {
            "comments.add", "comments.delete.own", "comments.edit.own",
            "dashboard.view", "export.pdf", "export.word",
            "meetings.create", "meetings.edit",
            "projects.edit",
            "records.comment.subsystemlead",
            "records.edit", "records.schedule.add", "records.schedule.edit",
            "team.manage"
        }, "VLASTNIK_PROJEKTU má dle seedu 14 permission keys po Fázi C");
    }

    [Fact]
    public async Task DbDrivenProjectGrants_ShouldCoverGEST()
    {
        await using var db = CreateSeededDb();
        var gestRole = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "GEST");

        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "G", Prijmeni = "E" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            OsobaId = 42, ProjektId = 777, RoleId = gestRole.Id,
            DatumPrirazeni = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        // Fáze C — Task C2: GEST dostává dashboard/export/comments klíče navíc
        var grantKeys = grants.Select(g => g.PermissionKey).OrderBy(x => x).ToArray();
        grantKeys.Should().BeEquivalentTo(new[]
        {
            "comments.add", "comments.delete.own", "comments.edit.own",
            "dashboard.view", "export.pdf", "export.word",
            "records.comment.subsystemlead"
        }, "GEST má po Fázi C 7 permission keys");
    }

    [Fact]
    public async Task DbDrivenProjectGrants_ShouldGiveHOST_ZeroGrants_InPhaseB()
    {
        await using var db = CreateSeededDb();
        var hostRole = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "HOST");

        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "H", Prijmeni = "O" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            OsobaId = 42, ProjektId = 777, RoleId = hostRole.Id,
            DatumPrirazeni = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        // Fáze C — Task C2: HOST dostává dashboard.view + export.* klíče
        var grantKeys = grants.Select(g => g.PermissionKey).OrderBy(x => x).ToArray();
        grantKeys.Should().BeEquivalentTo(new[]
        {
            "dashboard.view", "export.pdf", "export.word"
        }, "HOST má po Fázi C 3 read-only permission keys");
    }

    [Fact]
    public async Task DbDrivenProjectGrants_ShouldCoverFull_ADM_PROJ_Matrix()
    {
        await using var db = CreateSeededDb();
        var admRole = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "ADM_PROJ");

        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "A", Prijmeni = "P" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            OsobaId = 42, ProjektId = 777, RoleId = admRole.Id,
            DatumPrirazeni = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);
        var grantKeys = grants.Select(g => g.PermissionKey).OrderBy(x => x).ToArray();

        // Fáze C — Task C2: přidány dashboard/export/comments klíče (celkem 13)
        grantKeys.Should().BeEquivalentTo(new[]
        {
            "comments.add", "comments.delete.own", "comments.edit.own",
            "dashboard.view", "export.pdf", "export.word",
            "meetings.create", "meetings.edit",
            "records.comment.subsystemlead",
            "records.edit", "records.schedule.add", "records.schedule.edit",
            "team.manage"
        }, "ADM_PROJ má dle seedu 13 keys po Fázi C");
    }
}
