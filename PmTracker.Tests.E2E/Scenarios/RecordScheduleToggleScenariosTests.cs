using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Toggle Harmonogram na kartě záznamu (2026-07-13): switch přepne obsah karty mezi
/// detailem a harmonogramem (rozbalený rozpad), svislé menu drží překlik na záložku,
/// návrat z harmonogramu vrací kartu do pohledu záznam.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordScheduleToggleScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public RecordScheduleToggleScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    /// <summary>Zajistí task-record (kategorie Úkol) ve fixture projektu s vyplněnými kroky
    /// harmonogramu (plán 1-10, skutečnost 1-2). E2E dev seed nevkládá žádné záznamy, takže
    /// je vytváříme přímým SQL — idempotentně podle markeru názvu. Sloupce ověřeny přes
    /// sys.columns (memory feedback_legacy_columns_diacritics_raw_sql); cislo_viditelne_*
    /// mají DB default 0 (vynecháno).</summary>
    private async Task<int> EnsureTaskWithScheduleAsync()
    {
        const string marker = "E2E toggle harmonogram";
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        int recordId;
        await using (var upsert = connection.CreateCommand())
        {
            upsert.CommandText = """
                DECLARE @kat INT = (SELECT TOP (1) id FROM dbo.ciselnik_kategorii_zaznamu
                    WHERE kod = N'U' OR nazev LIKE N'%kol%' ORDER BY CASE WHEN kod = N'U' THEN 0 ELSE 1 END, id);
                DECLARE @stav INT = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_ukolu ORDER BY id);
                DECLARE @sub INT = (SELECT TOP (1) id FROM dbo.subsystemy ORDER BY id);
                DECLARE @owner INT = @o;

                IF NOT EXISTS (SELECT 1 FROM dbo.projekt_subsystemy
                    WHERE projekt_id = @p AND subsystem_id = @sub AND datum_odebrani IS NULL)
                    INSERT INTO dbo.projekt_subsystemy (projekt_id, subsystem_id, datum_prirazeni)
                    VALUES (@p, @sub, SYSUTCDATETIME());

                DECLARE @rid INT = (SELECT TOP (1) id FROM dbo.projektove_zaznamy
                    WHERE projekt_id = @p AND nazev = @marker ORDER BY id);
                IF @rid IS NULL
                BEGIN
                    DECLARE @num INT = (SELECT ISNULL(MAX(cislo_zaznamu), 0) + 1 FROM dbo.projektove_zaznamy WHERE projekt_id = @p);
                    INSERT INTO dbo.projektove_zaznamy
                        (projekt_id, kategorie_id, stav_ukolu_id, cislo_zaznamu, nazev, cil, popis,
                         vlastnik_id, datum_zalozeni, datum_ukonceni, subsystem_id)
                    VALUES
                        (@p, @kat, @stav, @num, @marker, @marker, @marker,
                         @owner, DATEADD(day, -30, CONVERT(DATE, GETDATE())), DATEADD(day, 45, CONVERT(DATE, GETDATE())), @sub);
                    SET @rid = CAST(SCOPE_IDENTITY() AS INT);
                END

                DELETE FROM dbo.zaznam_harmonogram_krok WHERE zaznam_id = @rid;
                DECLARE @start DATE = DATEADD(day, -30, CONVERT(DATE, GETDATE()));
                DECLARE @i INT = 1;
                WHILE @i <= 10
                BEGIN
                    INSERT INTO dbo.zaznam_harmonogram_krok
                        (zaznam_id, poradi, plan_datum, skutecnost_datum, skutecnost_zdroj, skutecnost_rezim, updated_at)
                    VALUES
                        (@rid, @i, DATEADD(day, @i * 7, @start),
                         CASE WHEN @i <= 2 THEN DATEADD(day, @i * 7 + 2, @start) ELSE NULL END,
                         CASE WHEN @i <= 2 THEN 2 ELSE 0 END, 2, SYSUTCDATETIME());
                    SET @i = @i + 1;
                END

                SELECT @rid;
                """;
            upsert.Parameters.AddWithValue("@p", _fixture.ProjectId);
            upsert.Parameters.AddWithValue("@o", _fixture.AdminOsobaId);
            upsert.Parameters.AddWithValue("@marker", marker);
            var result = await upsert.ExecuteScalarAsync();
            result.Should().NotBeNull("seed musí vrátit id záznamu");
            recordId = Convert.ToInt32(result);
        }

        return recordId;
    }

    // gov-form-switch je Stencil komponenta s vizuálně skrytým inputem (styl přebírá indikátor).
    // Playwright ho vidí jako „hidden", takže actionability klik (i Force) selže. DispatchEvent
    // „click" obchází actionability a vystřelí reálný click → toggle checkboxu → change (handler
    // recordScheduleView.js čte stav z inputu). Před tím počkáme na input v DOMu (hydratace).
    private static async Task ToggleViewAsync(IPage page, int recordId)
    {
        var host = page.Locator($".record-card[data-record-id='{recordId}'] gov-form-switch[data-record-view-switch]");
        // Počkat na hydrataci Stencil komponenty (třída 'hydrated') — jinak je interakce flaky.
        await Assertions.Expect(host).ToHaveClassAsync(new Regex(@"\bhydrated\b"));
        var input = host.Locator("input").First;
        await input.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        await input.DispatchEventAsync("click");
    }

    // gov-button host je pro Playwright „not visible" (Stencil) a vysoká karta v pohledu
    // harmonogram nikdy není „stable" (async render osy). DispatchEvent „click" obchází
    // actionability a bublá na document delegované handlery (bootstrap / recordActionsMenu /
    // crossTabNav). Pattern shodný s toggle switchem.
    private static async Task DispatchClickAsync(ILocator locator)
    {
        await locator.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        await locator.First.DispatchEventAsync("click");
    }

    [Fact]
    public async Task Switch_OnCollapsedCard_ExpandsAndShowsFullSchedule()
    {
        var recordId = await EnsureTaskWithScheduleAsync();
        var page = await _fixture.NewPageAsync();
        await page.SetViewportSizeAsync(1470, 956);
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");

        var card = page.Locator($".record-card[data-record-id='{recordId}']");
        await Assertions.Expect(card).ToHaveCountAsync(1);
        await Assertions.Expect(card).ToHaveClassAsync(new Regex(@"\bcollapsed\b"));

        await ToggleViewAsync(page, recordId);

        // Karta se rozbalila a je v pohledu harmonogram.
        await Assertions.Expect(card).ToHaveClassAsync(new Regex(@"record-card--schedule-view"));
        await Assertions.Expect(card).Not.ToHaveClassAsync(new Regex(@"\bcollapsed\b"));

        // Štítek Stíháme/Nestíháme + rozbalený rozpad kroků (R2). Rozpad je rozbalený rovnou —
        // .gantt-steps NEMÁ hidden atribut (v záložce Harmonogram by hidden měl).
        await Assertions.Expect(card.Locator("gov-tag")).ToHaveCountAsync(1);
        await Assertions.Expect(card.Locator(".gantt-steps")).ToHaveCountAsync(1);
        var stepsHidden = await card.Locator(".gantt-steps").First
            .EvaluateAsync<bool>("el => el.hasAttribute('hidden')");
        stepsHidden.Should().BeFalse("rozpad je na kartě rozbalený rovnou (R2)");
        await Assertions.Expect(card.Locator(".gantt-step-row")).Not.ToHaveCountAsync(0);

        // Detail i vyjádření jsou skryté (R3).
        var detailDisplay = await card.Locator("[data-record-detail-shell]")
            .EvaluateAsync<string>("el => getComputedStyle(el).display");
        detailDisplay.Should().Be("none");
        var commentsDisplay = await card.Locator(".record-comments-lazy")
            .EvaluateAsync<string>("el => getComputedStyle(el).display");
        commentsDisplay.Should().Be("none");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task CollapseAndExpand_KeepsScheduleView()
    {
        var recordId = await EnsureTaskWithScheduleAsync();
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");

        var card = page.Locator($".record-card[data-record-id='{recordId}']");
        await Assertions.Expect(card).ToHaveCountAsync(1);
        await ToggleViewAsync(page, recordId);
        await Assertions.Expect(card).ToHaveClassAsync(new Regex(@"record-card--schedule-view"));

        // Sbalit klikem na hlavičku a zase rozbalit.
        await DispatchClickAsync(card.Locator("[data-record-toggle]"));
        await Assertions.Expect(card).ToHaveClassAsync(new Regex(@"\bcollapsed\b"));
        await DispatchClickAsync(card.Locator("[data-record-toggle]"));
        await Assertions.Expect(card).Not.ToHaveClassAsync(new Regex(@"\bcollapsed\b"));

        // Pohled harmonogram přežil (R9/2).
        await Assertions.Expect(card).ToHaveClassAsync(new Regex(@"record-card--schedule-view"));

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Menu_GotoScheduleTab_AndGotoRecordResetsView()
    {
        var recordId = await EnsureTaskWithScheduleAsync();
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");

        var card = page.Locator($".record-card[data-record-id='{recordId}']");
        await Assertions.Expect(card).ToHaveCountAsync(1);

        // Do pohledu harmonogram.
        await ToggleViewAsync(page, recordId);
        await Assertions.Expect(card).ToHaveClassAsync(new Regex(@"record-card--schedule-view"));

        // Otevřít svislé menu — panel se mountuje do #floating-panel-root.
        await DispatchClickAsync(card.Locator("[data-record-menu-trigger]"));
        var menuItem = page.Locator($"[data-record-menu] [data-goto-schedule='{recordId}']");
        await Assertions.Expect(menuItem).ToBeVisibleAsync();

        // Překlik na záložku Harmonogram.
        await DispatchClickAsync(menuItem);
        await Assertions.Expect(page.Locator("[data-tab-panel='harmonogram'].active")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator($".schedule-card[data-schedule-record-id='{recordId}']")).ToHaveCountAsync(1);

        // Zpět na záznam přes goto-record → pohled se resetuje na záznam (R9/4).
        await DispatchClickAsync(page.Locator($".schedule-card[data-schedule-record-id='{recordId}'] [data-goto-record='{recordId}']"));
        await Assertions.Expect(page.Locator("[data-tab-panel='zaznamy'].active")).ToHaveCountAsync(1);
        await Assertions.Expect(card).Not.ToHaveClassAsync(new Regex(@"record-card--schedule-view"));

        await page.Context.CloseAsync();
    }
}
