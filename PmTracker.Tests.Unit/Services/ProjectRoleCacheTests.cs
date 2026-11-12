using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;

namespace PmTracker.Tests.Unit.Services;

/// <summary>
/// Scoped cache pro „lead-equivalent osoby na projekt/subsystem" lookup. Motivace:
/// CommentService + ProjectService.RecordCards měli duplikátní implementaci,
/// která se opakovaně volala v rámci jednoho requestu (3 dotazy × N záznamů).
/// Cache za request eliminuje redundantní dotazy a DRY unifikuje sémantiku.
/// </summary>
public sealed class ProjectRoleCacheTests
{
    [Fact]
    public async Task Get_SecondCallSameProjekt_ReturnsCachedInstance()
    {
        await using var db = CreateDb();
        await SeedLeadAssignmentAsync(db, projectId: 10, subsystemId: 100, osobaId: 5001);
        var cache = new ProjectRoleCache(db);

        var first = await cache.GetLeadEquivalentOsobaIdsBySubsystemAsync(10, CancellationToken.None);
        // Mezitím v DB přibude další záznam — pokud cache funguje, druhé volání jej nevidí.
        await SeedLeadAssignmentAsync(db, projectId: 10, subsystemId: 200, osobaId: 5002);
        var second = await cache.GetLeadEquivalentOsobaIdsBySubsystemAsync(10, CancellationToken.None);

        first.Should().BeSameAs(second, "druhé volání musí vrátit cached snapshot, ne nový dotaz");
        second.Should().ContainKey(100);
        second.Should().NotContainKey(200, "snapshot z prvního volání neobsahuje řádek, který přibyl později");
    }

    [Fact]
    public async Task Get_DifferentProjekt_FetchesIndependently()
    {
        await using var db = CreateDb();
        await SeedLeadAssignmentAsync(db, projectId: 10, subsystemId: 100, osobaId: 5001);
        await SeedLeadAssignmentAsync(db, projectId: 20, subsystemId: 200, osobaId: 5002);
        var cache = new ProjectRoleCache(db);

        var p10 = await cache.GetLeadEquivalentOsobaIdsBySubsystemAsync(10, CancellationToken.None);
        var p20 = await cache.GetLeadEquivalentOsobaIdsBySubsystemAsync(20, CancellationToken.None);

        p10.Should().ContainKey(100).And.NotContainKey(200);
        p20.Should().ContainKey(200).And.NotContainKey(100);
    }

    [Fact]
    public async Task Get_NoLeadRolesInCiselnik_ReturnsEmpty()
    {
        await using var db = CreateDb();
        // Žádná ciselnik_roli_subsystemu s kódem lead/deputy_lead → prázdná dict.
        var cache = new ProjectRoleCache(db);

        var result = await cache.GetLeadEquivalentOsobaIdsBySubsystemAsync(10, CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForSubsystem_ReturnsSubsetFromBySubsystem()
    {
        await using var db = CreateDb();
        await SeedLeadAssignmentAsync(db, projectId: 10, subsystemId: 100, osobaId: 5001);
        await SeedLeadAssignmentAsync(db, projectId: 10, subsystemId: 200, osobaId: 5002);
        var cache = new ProjectRoleCache(db);

        var ids = await cache.GetLeadEquivalentOsobaIdsAsync(10, 100, CancellationToken.None);

        ids.Should().Equal(5001);
    }

    private static Task SeedLeadAssignmentAsync(PmTrackerDbContext db, int projectId, int subsystemId, int osobaId)
        => SeedRoleAssignmentAsync(db, projectId, subsystemId, osobaId, SubsystemRoleCodes.Lead);

    private static readonly IReadOnlyDictionary<string, int> RoleIds = new Dictionary<string, int>
    {
        [SubsystemRoleCodes.Lead] = 1,
        [SubsystemRoleCodes.DeputyLead] = 2,
        [SubsystemRoleCodes.Methodik] = 3
    };

    private static async Task SeedRoleAssignmentAsync(
        PmTrackerDbContext db, int projectId, int subsystemId, int osobaId, string roleCode, DateTime? datumOdebrani = null)
    {
        if (!db.CiselnikRoliSubsystemu.Any(x => x.Kod == roleCode))
        {
            db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity
            {
                Id = RoleIds[roleCode],
                Kod = roleCode,
                Nazev = roleCode
            });
            await db.SaveChangesAsync();
        }

        var roleId = db.CiselnikRoliSubsystemu.First(x => x.Kod == roleCode).Id;

        var projektSubsystem = db.ProjektSubsystemy.FirstOrDefault(x =>
            x.ProjektId == projectId && x.SubsystemId == subsystemId);
        if (projektSubsystem is null)
        {
            projektSubsystem = new ProjektSubsystemEntity
            {
                Id = projectId * 1000 + subsystemId,
                ProjektId = projectId,
                SubsystemId = subsystemId
            };
            db.ProjektSubsystemy.Add(projektSubsystem);
            await db.SaveChangesAsync();
        }

        db.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            ProjektSubsystemId = projektSubsystem.Id,
            OsobaId = osobaId,
            RoleSubsystemuId = roleId,
            DatumPrirazeni = DateTime.UtcNow.AddDays(-10),
            DatumOdebrani = datumOdebrani
        });
        await db.SaveChangesAsync();
    }

    /// <summary>5001 vedoucí, 5002 zástupce, 5003 metodik subsystému 100; 5004 metodik subsystému 200.</summary>
    private static async Task SeedAllRolesAsync(PmTrackerDbContext db, DateTime? metodik5003Odebran = null)
    {
        await SeedRoleAssignmentAsync(db, 10, 100, 5001, SubsystemRoleCodes.Lead);
        await SeedRoleAssignmentAsync(db, 10, 100, 5002, SubsystemRoleCodes.DeputyLead);
        await SeedRoleAssignmentAsync(db, 10, 100, 5003, SubsystemRoleCodes.Methodik, metodik5003Odebran);
        await SeedRoleAssignmentAsync(db, 10, 200, 5004, SubsystemRoleCodes.Methodik);
    }

    // Spec 2026-11-10 R3: pro vyjádření se počítají všechny tři subsystémové role, metodik včetně.

    [Fact]
    public async Task Subsystemove_role_obsahuji_vedouciho_zastupce_i_metodika_jen_sveho_subsystemu()
    {
        await using var db = CreateDb();
        await SeedAllRolesAsync(db);
        var cache = new ProjectRoleCache(db);

        (await cache.GetSubsystemRoleOsobaIdsAsync(10, 100)).Should().Equal(5001, 5002, 5003);
        (await cache.GetSubsystemRoleOsobaIdsAsync(10, 200)).Should().Equal(5004);
        (await cache.GetSubsystemRoleOsobaIdsAsync(10, 300)).Should().BeEmpty();
    }

    [Fact]
    public async Task Seznam_vedoucich_dal_metodika_neobsahuje()
    {
        await using var db = CreateDb();
        await SeedAllRolesAsync(db);
        var cache = new ProjectRoleCache(db);

        (await cache.GetLeadEquivalentOsobaIdsAsync(10, 100)).Should().Equal(5001, 5002);
        (await cache.GetLeadEquivalentOsobaIdsAsync(10, 200)).Should().BeEmpty();
    }

    [Fact]
    public async Task Odebrany_metodik_neni_v_seznamu()
    {
        await using var db = CreateDb();
        await SeedAllRolesAsync(db, metodik5003Odebran: DateTime.UtcNow.AddDays(-1));
        var cache = new ProjectRoleCache(db);

        (await cache.GetSubsystemRoleOsobaIdsAsync(10, 100)).Should().Equal(5001, 5002);
    }

    [Fact]
    public async Task Subsystemove_role_druhe_volani_vrati_tutez_instanci()
    {
        await using var db = CreateDb();
        await SeedAllRolesAsync(db);
        var cache = new ProjectRoleCache(db);

        var first = await cache.GetSubsystemRoleOsobaIdsBySubsystemAsync(10);
        await SeedRoleAssignmentAsync(db, 10, 300, 5009, SubsystemRoleCodes.Methodik);
        var second = await cache.GetSubsystemRoleOsobaIdsBySubsystemAsync(10);

        first.Should().BeSameAs(second);
        second.Should().NotContainKey(300);
    }

    private static PmTrackerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(opts);
    }
}
