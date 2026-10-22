using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-10-22 R13: ✕ u plánu kroku v editoru vyprázdní datum a trvání kroku ukáže „–“.
/// Další krok se přepočte od posledního kroku s plánem a po uložení je plán v databázi prázdný.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class PlanDateClearScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public PlanDateClearScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private PmTrackerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(_fixture.Database.ConnectionString).Options);

    /// <summary>Úkol ve fixture projektu založený před 40 dny, plán kroku k = založení + 7·k.</summary>
    private async Task<(int RecordId, DateTime Zalozeni)> SeedAsync()
    {
        await using var db = CreateDbContext();
        var subsystemId = await db.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == _fixture.ProjectId && x.DatumOdebrani == null)
            .OrderBy(x => x.SubsystemId).Select(x => x.SubsystemId).FirstAsync();
        var categoryId = await db.CiselnikKategoriiZaznamu.Where(x => x.Kod == "U").Select(x => x.Id).FirstAsync();
        var stateId = await db.CiselnikStavuUkolu.Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var number = (await db.ProjektoveZaznamy.Where(x => x.ProjektId == _fixture.ProjectId)
            .Select(x => (int?)x.CisloZaznamu).MaxAsync() ?? 0) + 1;
        var zalozeni = DateTime.Today.AddDays(-40);
        var record = new ProjektovyZaznamEntity
        {
            ProjektId = _fixture.ProjectId, KategorieId = categoryId, StavUkoluId = stateId, CisloZaznamu = number,
            Nazev = "E2E smazani planu", Cil = "E2E smazani planu", Popis = "E2E smazani planu",
            VlastnikId = _fixture.AdminOsobaId, DatumZalozeni = zalozeni, DatumUkonceni = DateTime.Today.AddDays(60),
            SubsystemId = subsystemId
        };
        db.ProjektoveZaznamy.Add(record);
        await db.SaveChangesAsync();
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = record.Id,
                Poradi = (byte)poradi,
                PlanDatum = zalozeni.AddDays(7 * poradi),
                SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                    ? (byte)SkutecnostRezimEnum.Manual
                    : (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return (record.Id, zalozeni);
    }

    private async Task CleanupAsync(int recordId)
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.zaznam_edit_zamek WHERE zaznam_id = {recordId}");
        await db.ZaznamHarmonogramKroky.Where(x => x.ZaznamId == recordId).ExecuteDeleteAsync();
        await db.ProjektoveZaznamy.Where(x => x.Id == recordId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Editor_ClearPlan_ShowsDash_RecalculatesNextStep_AndSavesEmptyPlan()
    {
        var (recordId, zalozeni) = await SeedAsync();
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
            var row3 = page.Locator("tr[data-schedule-step-row][data-step-index='3']");
            // Řádek má i pole skutečnosti (u existujícího záznamu taky s ✕) — kotvit na pole plánu.
            var plan3 = row3.Locator("pm-date-field[data-schedule-plan-step]");
            var clear = plan3.Locator("[data-app-date-clear]");
            await Expect(clear).ToHaveCountAsync(1);
            await Expect(clear).ToBeEnabledAsync();

            await clear.DispatchEventAsync("click");

            await Expect(plan3.Locator("input[data-app-date-value]")).ToHaveValueAsync(string.Empty);
            await Expect(row3.Locator("[data-schedule-duration-readonly]")).ToHaveTextAsync("–");
            await Expect(page.Locator("tr[data-schedule-step-row][data-step-index='4'] [data-schedule-duration-readonly]"))
                .ToHaveTextAsync("14 dnů");

            var saveTask = page.WaitForResponseAsync(response =>
                response.Request.Method == "POST"
                && response.Url.Contains("/Zaznamy/Save", StringComparison.OrdinalIgnoreCase));
            await page.Locator("form[data-record-editor-form]").EvaluateAsync("form => form.requestSubmit()");
            var save = await saveTask;
            // Tělo odpovědi číst nejde — editor po uložení naviguje a Chromium ho zahodí.
            save.Ok.Should().BeTrue($"uložení vrátilo HTTP {save.Status}");

            await using var db = CreateDbContext();
            var plans = await db.ZaznamHarmonogramKroky.AsNoTracking()
                .Where(x => x.ZaznamId == recordId)
                .ToDictionaryAsync(x => (int)x.Poradi, x => x.PlanDatum);
            plans[3].Should().BeNull("✕ plán kroku 3 smazal");
            plans[2].Should().Be(zalozeni.AddDays(14));
            plans[4].Should().Be(zalozeni.AddDays(28));
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(recordId);
        }
    }
}
