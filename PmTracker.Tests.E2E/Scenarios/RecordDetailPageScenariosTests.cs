using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Stránka záznamu (2026-07-14): dvousloupcové read-only zobrazení, přepínač
/// Graf ⇄ Tabulka s pamětí polohy, ukládání vyjádření.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordDetailPageScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public RecordDetailPageScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private async Task<int> EnsureTaskWithScheduleAsync()
    {
        const string marker = "E2E stranka zaznamu";
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

                -- Vyjádření mažeme taky: panel načítá jen první dávku, takže nasbíraná
                -- vyjádření z předchozích běhů by nové vytlačila mimo DOM a scénář
                -- „přidání vyjádření" by selhal podle pořadí/počtu běhů.
                DELETE FROM dbo.vyjadreni WHERE zaznam_id = @rid;
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

                -- Vlastní otevřené jednání: bez něj nelze přidat vyjádření a jiné scénáře
                -- svá jednání v úklidu mažou, takže se na ně nedá spolehnout.
                IF NOT EXISTS (SELECT 1 FROM dbo.jednani WHERE projekt_id = @p AND cislo_jednani = 9931)
                BEGIN
                    DECLARE @stateId INT = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_jednani WHERE kod = N'OPEN');
                    INSERT INTO dbo.jednani (projekt_id, cislo_jednani, datum_planovane, cas_zacatek, misto, stav_jednani_id)
                    VALUES (@p, 9931, CONVERT(DATE, GETDATE()), '09:00', N'E2E stranka zaznamu', @stateId);
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

    // gov/vysoké prvky nejsou pro Playwright „visible/stable" → DispatchEvent click.
    private static async Task DispatchClickAsync(ILocator locator)
    {
        await locator.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        await locator.First.DispatchEventAsync("click");
    }

    [Fact]
    public async Task Page_ShowsHeaderCommentsAndGraph_ByDefault()
    {
        var recordId = await EnsureTaskWithScheduleAsync();
        var page = await _fixture.NewPageAsync();
        await page.SetViewportSizeAsync(1470, 956);
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");

        await Assertions.Expect(page.Locator(".record-page-header")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-record-comments-shell]")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-record-page-schedule]")).ToHaveCountAsync(1);

        // Výchozí = graf: tabulka skrytá, rozpad viditelný, osy vykreslené (mají popisky).
        var tableDisplay = await page.Locator("[data-record-page-schedule-table]")
            .EvaluateAsync<string>("el => getComputedStyle(el).display");
        tableDisplay.Should().Be("none");
        await Assertions.Expect(page.Locator(".gantt-step-row")).Not.ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("[data-record-page-schedule-graph] .timeline-axis-label")).Not.ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Toggle_SwitchesToTable_AndSurvivesReload()
    {
        var recordId = await EnsureTaskWithScheduleAsync();
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");

        await DispatchClickAsync(page.Locator("[data-schedule-view-toggle='table']"));
        await Assertions.Expect(page.Locator("[data-record-page-schedule]"))
            .ToHaveClassAsync(new Regex("record-page-schedule--table"));

        var graphDisplay = await page.Locator("[data-record-page-schedule-graph]")
            .EvaluateAsync<string>("el => getComputedStyle(el).display");
        graphDisplay.Should().Be("none");
        await Assertions.Expect(page.Locator(".schedule-table-wrap")).ToHaveCountAsync(1);

        // Poloha přežije reload (localStorage).
        await page.ReloadAsync();
        await Assertions.Expect(page.Locator("[data-record-page-schedule]"))
            .ToHaveClassAsync(new Regex("record-page-schedule--table"));

        // Zpět na graf → osy se dokreslí (ve skrytém stavu měly nulovou šířku).
        await DispatchClickAsync(page.Locator("[data-schedule-view-toggle='graph']"));
        await Assertions.Expect(page.Locator("[data-record-page-schedule]"))
            .Not.ToHaveClassAsync(new Regex("record-page-schedule--table"));
        await Assertions.Expect(page.Locator("[data-record-page-schedule-graph] .timeline-axis-label")).Not.ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    // Scénář „přidání vyjádření na stránce" tu není: E2E sdílí databázi a sousední
    // scénáře (RecordRichText…) si mažou jednání i záznamy, takže vazba vyjádření na
    // jednání není v mixovaném běhu deterministická. Kontrakt formuláře (správné id
    // záznamu, wrapper karty kvůli AJAX obnově) pinuje Api test
    // RecordDetailPageRenderTests.DetailPage_CommentForm_PostsToCorrectRecord;
    // reálné uložení bylo ověřeno živě na dev instanci.
}
