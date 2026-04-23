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

    private static async Task SeedLeadAssignmentAsync(
        PmTrackerDbContext db, int projectId, int subsystemId, int osobaId)
    {
        // Ciselnik s lead kódem (deduplikace kvůli idempotenci testu).
        if (!db.CiselnikRoliSubsystemu.Any(x => x.Kod == SubsystemRoleCodes.Lead))
        {
            db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity
            {
                Id = 1,
                Kod = SubsystemRoleCodes.Lead,
                Nazev = "Lead"
            });
            await db.SaveChangesAsync();
        }

        var leadRoleId = db.CiselnikRoliSubsystemu.First(x => x.Kod == SubsystemRoleCodes.Lead).Id;

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
            RoleSubsystemuId = leadRoleId,
            DatumPrirazeni = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static PmTrackerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(opts);
    }
}
