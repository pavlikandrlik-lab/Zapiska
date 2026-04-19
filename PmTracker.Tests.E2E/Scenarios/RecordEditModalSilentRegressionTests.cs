using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Regrese 2026-04-19: site.bundle.js + modules obsahoval dvě silent regressions
/// v JS tree (bundle rename neviděl import omissions v sources):
/// 1. recordEditor.js nevolal import { getActiveModalContainer } → ReferenceError
///    při pokusu o zavření dirty modal → close guard nedokončil → "modal nereaguje"
/// 2. ui.js odkazoval recordEditorState bez importu → scroll/resize throw ReferenceError
///    → chooser se nepozicuje + event handler se špatně chová
///
/// Oprava: recordEditor.js doplnit import getActiveModalContainer; ui.js nahrazena
/// cyklická závislost runtime registry (registerFloatingChooser/unregisterFloatingChooser).
///
/// Tento test ověřuje že stránka s otevřeným editor modalem nehlásí žádné pageerror
/// a že close flow funguje end-to-end (včetně dirty → close guard → Zahodit → modal dismiss).
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordEditModalSilentRegressionTests
{
    private readonly E2ETestFixture _fixture;

    public RecordEditModalSilentRegressionTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RecordEditModal_DirtyClose_ShouldShowGuardAndZahoditZavreModal_BezJsError()
    {
        await EnsureRecordExistsAsync();

        var page = await _fixture.NewPageAsync();
        var pageErrors = new List<string>();
        page.PageError += (_, err) => pageErrors.Add(err);

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();

        var editButton = page.Locator("[data-record-editor-url]").First;
        (await editButton.CountAsync()).Should().BeGreaterThan(0, "seed musí poskytnout aspoň 1 editovatelný záznam");
        await editButton.ClickAsync();

        var modal = page.Locator(".modal-overlay");
        await Expect(modal.Locator("form[data-record-editor-form='true']")).ToBeVisibleAsync();

        // Dirty form
        var nazev = modal.Locator("input[name='Nazev']");
        var currentValue = await nazev.InputValueAsync();
        await nazev.FillAsync(currentValue + " edit");

        // Click X close
        var xClose = modal.GetByRole(AriaRole.Button, new() { Name = "Zavřít dialog" });
        (await xClose.CountAsync()).Should().Be(1);
        await xClose.ClickAsync();

        // Close guard must appear (dirty form)
        var guard = page.Locator("[data-record-editor-close-guard]");
        await Expect(guard).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 3000 });

        // Zahodit změny → modal closes
        await guard.GetByRole(AriaRole.Button, new() { Name = "Zahodit změny" }).ClickAsync();
        await Expect(page.Locator(".modal-overlay")).ToHaveCountAsync(0);

        pageErrors.Should().BeEmpty(
            "silent regressions (recordEditorState / getActiveModalContainer not defined) nesmí v JS tree vznikat");

        await page.Context.CloseAsync();
    }

    private async Task EnsureRecordExistsAsync()
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"SELECT COUNT(*) FROM dbo.projektove_zaznamy WHERE projekt_id = {_fixture.ProjectId}";
        var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
        if (count > 0)
        {
            return;
        }

        await using var seedCommand = connection.CreateCommand();
        seedCommand.CommandText = $"""
            DECLARE @subsystemId int = (SELECT TOP 1 subsystem_id FROM dbo.projekt_subsystemy WHERE projekt_id = {_fixture.ProjectId} AND datum_odebrani IS NULL ORDER BY id);
            DECLARE @kategorieId int = (SELECT TOP 1 id FROM dbo.ciselnik_kategorii_zaznamu WHERE kod = 'INFO');
            IF @kategorieId IS NULL SET @kategorieId = (SELECT TOP 1 id FROM dbo.ciselnik_kategorii_zaznamu ORDER BY id);
            INSERT INTO dbo.projektove_zaznamy(projekt_id, kategorie_id, aktualni_typ_ukolu_id, stav_ukolu_id, cislo_zaznamu, cislo_viditelne, cislo_viditelne_typ, cislo_viditelne_a, cislo_viditelne_b, cislo_jednani_zdroj_id, nazev, cil, popis, vlastnik_id, datum_zalozeni, datum_ukonceni, subsystem_id, harmonogram_sablona_verze)
            VALUES ({_fixture.ProjectId}, @kategorieId, NULL, NULL, 1, N'1', 0, 0, 0, NULL, N'E2E regrese record', N'Cíl', N'Popis', {_fixture.AdminOsobaId}, GETDATE(), DATEADD(day,14,GETDATE()), @subsystemId, 1);
            """;
        seedCommand.CommandTimeout = 60;
        await seedCommand.ExecuteNonQueryAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);
}
