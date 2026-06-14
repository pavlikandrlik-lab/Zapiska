using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Vyzvy;

namespace PmTracker.Tests.Unit.ProjectDashboard;

/// <summary>
/// Regression: C-3 SQL-level year filter — BuildStatisticsPanelAsync must only retrieve
/// records whose DatumUkonceni falls within the prev-year/next-year buffer, not every
/// record for the project.
/// </summary>
public sealed class ProjectDashboardStatisticsYearFilterTests
{
    private const int ProjectId = 1;
    private const int Year = 2024;

    /// <summary>
    /// Seeds 5 records for the same project:
    ///   - 2 within the year range (2024)
    ///   - 3 outside (2021, 2022, 2027) — outside the [2023-01-01 .. 2025-12-30] buffer
    /// Asserts that the returned KPI totals (Preneseno = records planned for 2024 not completed)
    /// are consistent with only the 2 in-range records being considered, not all 5.
    /// </summary>
    [Fact]
    public async Task BuildStatisticsPanelAsync_OnlyFetchesRecordsInYearRange()
    {
        await using var db = CreateDb();
        await SeedRequiredLookupDataAsync(db);
        await SeedRecordsAsync(db);

        var service = CreateService(db);

        var result = await service.BuildStatisticsPanelAsync(ProjectId, Year);

        // 2 records in range, neither is in a final state (no CiselnikStavuUkolu seeded with IsFinal=true)
        // so both appear as "Preneseno" (carry-over: planned but not completed)
        result.Kpi.Preneseno.Should().Be(2,
            because: "only 2 records fall within the year range; the 3 outside-range records must not be counted");

        // Quarters for 2024: the 2 records are distributed across Q1 and Q3
        var plannedTotal = result.Quarters.Sum(q => q.PlannedCompletions);
        plannedTotal.Should().Be(2,
            because: "all 4 quarters combined should total the 2 in-range records");
    }

    private static PmTrackerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(opts);
    }

    private static async Task SeedRequiredLookupDataAsync(PmTrackerDbContext db)
    {
        // Task category required for the WHERE clause
        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity
        {
            Id = 10, Kod = "UKOL", Nazev = "Úkol"
        });

        // No final states seeded — all records will be "carry-over" (Preneseno)

        // Subsystem required to avoid null-ref in subsystem stats grouping
        db.Subsystemy.Add(new SubsystemEntity
        {
            Id = 1, Kod = "S1", Nazev = "Subsystem 1"
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedRecordsAsync(PmTrackerDbContext db)
    {
        // 2 records IN range (2024) — DatumUkonceni within [2023-01-01 .. 2025-12-30]
        db.ProjektoveZaznamy.AddRange(
            MakeRecord(id: 1, ukonceni: new DateTime(2024, 3, 15)),   // Q1 2024 — in range
            MakeRecord(id: 2, ukonceni: new DateTime(2024, 9, 30))    // Q3 2024 — in range
        );

        // 3 records OUTSIDE range — before 2023 or after 2025
        db.ProjektoveZaznamy.AddRange(
            MakeRecord(id: 3, ukonceni: new DateTime(2021, 6, 1)),    // too old
            MakeRecord(id: 4, ukonceni: new DateTime(2022, 12, 31)),  // too old
            MakeRecord(id: 5, ukonceni: new DateTime(2027, 1, 1))     // too future
        );

        await db.SaveChangesAsync();
    }

    private static ProjektovyZaznamEntity MakeRecord(int id, DateTime ukonceni) =>
        new()
        {
            Id = id,
            ProjektId = ProjectId,
            KategorieId = 10, // matches UKOL category seeded above
            CisloZaznamu = id,
            CisloViditelne = $"U{id}",
            Nazev = $"Record {id}",
            VlastnikId = 1,
            DatumZalozeni = ukonceni.AddMonths(-1),
            DatumUkonceni = ukonceni,
            SubsystemId = 1
        };

    private static ProjectDashboardService CreateService(PmTrackerDbContext db)
    {
        var vyzvaServiceMock = new Mock<IVyzvaService>();
        var vyzvyPanelBuilder = new VyzvyPanelBuilder(vyzvaServiceMock.Object, db);
        // Statistics panel nevolá query service, stačí inert mock (Plán 5 Sprint B Task 4).
        var isQueryServiceMock = new Mock<PmTracker.ServiceDesk.Contracts.IInformacniSystemQueryService>();

        return new ProjectDashboardService(db, vyzvyPanelBuilder, isQueryServiceMock.Object);
    }
}
