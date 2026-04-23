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

        var grantKeys = grants.Select(g => g.PermissionKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        // Per-action redesign 2026-04-23: VLASTNIK_PROJEKTU má 59 cílových + 5 deprecated klíčů.
        grantKeys.Should().BeEquivalentTo(ProjectExecutiveAllKeys(),
            "VLASTNIK_PROJEKTU má kompletní project-executive matrix po per-action redesignu");
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

        // Per-action redesign 2026-04-23: GEST má 15 cílových + 3 deprecated klíče.
        var grantKeys = grants.Select(g => g.PermissionKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        grantKeys.Should().BeEquivalentTo(new[]
        {
            "comments.add", "comments.delete.own", "comments.edit.own",
            "dashboard.nes.view", "dashboard.records.view", "dashboard.statistics.view",
            "dashboard.view", "dashboard.vyzvy.view",
            "export.pdf.jednani", "export.pdf.projekt", "export.pdf.ukol",
            "export.word.jednani", "export.word.projekt", "export.word.ukol",
            "search.index",
            // Deprecated (kompat F1–F6):
            "records.comment.subsystemlead", "export.pdf", "export.word"
        }, "GEST má komentátorský balíček + read-only po per-action redesignu");
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

        // Per-action redesign 2026-04-23: HOST má 12 cílových + 2 deprecated klíče.
        var grantKeys = grants.Select(g => g.PermissionKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        grantKeys.Should().BeEquivalentTo(new[]
        {
            "dashboard.nes.view", "dashboard.records.view", "dashboard.statistics.view",
            "dashboard.view", "dashboard.vyzvy.view",
            "export.pdf.jednani", "export.pdf.projekt", "export.pdf.ukol",
            "export.word.jednani", "export.word.projekt", "export.word.ukol",
            "search.index",
            // Deprecated (kompat F1–F6):
            "export.pdf", "export.word"
        }, "HOST je read-only pozorovatel po per-action redesignu");
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
        var grantKeys = grants.Select(g => g.PermissionKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        // Per-action redesign 2026-04-23: ADM_PROJ = project executive matrix.
        grantKeys.Should().BeEquivalentTo(ProjectExecutiveAllKeys(),
            "ADM_PROJ má kompletní project-executive matrix po per-action redesignu");
    }

    /// <summary>
    /// Cílová sada klíčů pro VLASTNIK_PROJEKTU / ADM_PROJ / PROJ_MAN (= 59 cílových
    /// klíčů per-action matice + 5 deprecated klíčů, které zůstanou v seedu do F7).
    /// </summary>
    private static IEnumerable<string> ProjectExecutiveAllKeys() => new[]
    {
        // 59 cílových klíčů z matice (target state):
        "comments.add", "comments.delete.any", "comments.delete.own",
        "comments.edit.any", "comments.edit.own",
        "dashboard.nes.view", "dashboard.records.view", "dashboard.statistics.view",
        "dashboard.view", "dashboard.vyzvy.view",
        "export.pdf.jednani", "export.pdf.projekt", "export.pdf.ukol",
        "export.word.jednani", "export.word.projekt", "export.word.ukol",
        "externiodkazy.sync",
        "meetings.attendance.edit", "meetings.create", "meetings.delete", "meetings.edit",
        "meetings.notes.edit", "meetings.notes.subsystemlead", "meetings.participant.add",
        "meetings.status.change",
        "proposals.accept", "proposals.edit.any", "proposals.edit.own",
        "proposals.record.create", "proposals.reject", "proposals.schedule.create",
        "proposals.takeover",
        "records.assign.meeting", "records.create", "records.delete", "records.edit",
        "records.schedule.edit",
        "schedule.preview",
        "search.index",
        "team.candidates.search", "team.member.add", "team.member.remove",
        "team.role.assign", "team.role.deactivate",
        "team.subsystem.create", "team.subsystem.deactivate", "team.subsystem.reorder",
        "team.subsystem.role.assign", "team.subsystem.role.deactivate",
        "vyjadreni.modal.open", "vyjadreni.refresh", "vyjadreni.reharvest",
        "vyjadreni.vazba.create", "vyjadreni.vazba.delete",
        "vyzvy.create", "vyzvy.pnf.assign", "vyzvy.pnf.reassign",
        "vyzvy.state.change", "vyzvy.word.export",
        // 5 deprecated klíčů (kompat F1–F6):
        "records.schedule.add", "records.comment.subsystemlead", "team.manage",
        "export.pdf", "export.word"
    };
}
