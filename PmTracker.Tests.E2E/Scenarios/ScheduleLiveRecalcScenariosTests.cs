using System.Text.RegularExpressions;
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
/// Spec 2026-10-24 R9, R10: editor harmonogramu po ✕ i po výběru data hned přepočítá stav
/// (u aktuálního kroku bez plánu k celkovému termínu) a kalendář plánu vypne dny mimo pořadí
/// podle nejbližšího kroku s plánem.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ScheduleLiveRecalcScenariosTests
{
    private const string Krok1 = "Aktuální krok: 1. priprava zadani dodavateli";
    private readonly E2ETestFixture _fixture;

    public ScheduleLiveRecalcScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private PmTrackerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(_fixture.Database.ConnectionString).Options);

    /// <summary>Úkol založený před 40 dny, termín za 60 dní, plán kroku k = založení + 7·k, krok <paramref name="bezPlanu"/> bez plánu.</summary>
    private async Task<(int RecordId, DateTime Zalozeni)> SeedAsync(int? bezPlanu = null)
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
            Nazev = "E2E zivy prepocet", Cil = "E2E zivy prepocet", Popis = "E2E zivy prepocet",
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
                PlanDatum = poradi == bezPlanu ? null : zalozeni.AddDays(7 * poradi),
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

    private async Task<IPage> OpenEditorScheduleAsync(int recordId)
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        await page.Locator("pm-tabs [key='schedule']").First.ClickAsync();
        return page;
    }

    private static ILocator PlanField(IPage page, int poradi)
        => page.Locator($"tr[data-schedule-step-row][data-step-index='{poradi}'] pm-date-field[data-schedule-plan-step]");

    /// <summary>Otevře kalendář pole a šipkami ‹ › přejde na měsíc daného dne; vrátí tlačítko dne.</summary>
    private static async Task<ILocator> OpenCalendarAtAsync(IPage page, ILocator dateField, DateTime day)
    {
        await dateField.Locator("[data-app-date-open]").DispatchEventAsync("click");
        var panel = page.Locator("[data-app-date-panel]:not([hidden])");
        await Expect(panel).ToHaveCountAsync(1);
        var shownMonth = int.Parse(await panel.Locator("[data-app-date-month]").InputValueAsync());
        var shownYear = int.Parse(await panel.Locator("[data-app-date-year]").InputValueAsync());
        var diff = (day.Year * 12 + day.Month - 1) - (shownYear * 12 + shownMonth);
        for (var i = 0; i < Math.Abs(diff); i++)
        {
            await panel.Locator(diff > 0 ? "[data-app-date-next]" : "[data-app-date-prev]").DispatchEventAsync("click");
        }
        return panel.Locator($"button.app-date-day[data-iso='{day:yyyy-MM-dd}']");
    }

    [Fact]
    public async Task Editor_ClearCurrentStepPlan_StateCountsToDeadline_PickDate_BackToPlan()
    {
        var (recordId, zalozeni) = await SeedAsync();
        var page = await OpenEditorScheduleAsync(recordId);
        try
        {
            var state = page.Locator("[data-schedule-block][data-schedule-mode='record-editor'] [data-schedule-summary-state]");
            var statusLine = page.Locator("[data-schedule-block][data-schedule-mode='record-editor'] .schedule-status-line");
            await Expect(state).ToHaveTextAsync($"{Krok1} · skluz +33 dní");

            await PlanField(page, 1).Locator("[data-app-date-clear]").DispatchEventAsync("click");

            await Expect(state).ToHaveTextAsync($"{Krok1} · plán není zadán · celkový termín: zbývá 60 dní");
            await Expect(statusLine).ToHaveClassAsync(new Regex("\\bok\\b"));

            var day = await OpenCalendarAtAsync(page, PlanField(page, 1), zalozeni.AddDays(10));
            await Expect(day).ToBeEnabledAsync();
            await day.DispatchEventAsync("click");

            await Expect(PlanField(page, 1).Locator("input[data-app-date-value]")).ToHaveValueAsync($"{zalozeni.AddDays(10):yyyy-MM-dd}");
            await Expect(state).ToHaveTextAsync($"{Krok1} · skluz +30 dní");
            await Expect(statusLine).ToHaveClassAsync(new Regex("\\blate\\b"));
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(recordId);
        }
    }

    [Fact]
    public async Task Editor_PlanCalendar_BoundsSkipStepWithoutPlan_AndFollowChanges()
    {
        var (recordId, zalozeni) = await SeedAsync(bezPlanu: 2);
        var page = await OpenEditorScheduleAsync(recordId);
        try
        {
            var plan3 = PlanField(page, 3);
            await Expect(plan3).ToHaveAttributeAsync("min", $"{zalozeni.AddDays(7):yyyy-MM-dd}");
            await Expect(plan3).ToHaveAttributeAsync("max", $"{zalozeni.AddDays(28):yyyy-MM-dd}");
            (await PlanField(page, 1).GetAttributeAsync("min")).Should().BeNull("krok 1 nemá dřívější krok");

            var beforePlan1 = await OpenCalendarAtAsync(page, plan3, zalozeni.AddDays(6));
            await Expect(beforePlan1).ToBeDisabledAsync();
            await Expect(page.Locator($"[data-app-date-panel]:not([hidden]) button.app-date-day[data-iso='{zalozeni.AddDays(7):yyyy-MM-dd}']"))
                .ToBeEnabledAsync();
            await page.Keyboard.PressAsync("Escape");

            await PlanField(page, 4).Locator("[data-app-date-clear]").DispatchEventAsync("click");

            await Expect(plan3).ToHaveAttributeAsync("max", $"{zalozeni.AddDays(35):yyyy-MM-dd}");
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(recordId);
        }
    }
}
