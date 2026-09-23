using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>A8 (2026-07-08): dirty stránkový editor — šipka zpět i breadcrumb ← vyvolají
/// aplikační dialog; čistý editor projde bez dotazu; žádný nativní browser dialog.
/// Testy čekají na podmínky (trap armed = history.state.pmEditorTrap, snapshot init),
/// ne na časy — init modulů je asynchronní.</summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordEditorHistoryBackScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public RecordEditorHistoryBackScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private string EditorUrl() => $"{_fixture.BaseUrl}/Zaznamy/Create?projektId={_fixture.ProjectId}&asUser={_fixture.AdminOsobaId}";
    private string ProjectUrl() => $"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}";
    private static ILocatorAssertions Expect(ILocator l) => Assertions.Expect(l);

    private static async Task WaitForTrapArmedAsync(IPage page)
    {
        // Trap armed = marker atribut (history.state NELZE použít — pm-tabs ho nuluje
        // replaceState(null); entry sentinelu ale zůstává) + dirty baseline snapshot.
        await page.WaitForFunctionAsync(
            @"() => {
                const f = document.querySelector('form[data-record-editor-form=""true""][data-record-editor-presentation=""page""]');
                return !!f && !!f.dataset.recordEditorSnapshot
                    && document.documentElement.getAttribute('data-pm-editor-trap-armed') === 'true';
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 10000 });
    }

    private static List<string> CaptureErrors(IPage page)
    {
        var errors = new List<string>();
        page.PageError += (_, e) => errors.Add(e);
        return errors;
    }

    [Fact]
    public async Task DirtyEditor_BrowserBack_ShowsAppDialog_StayAndLeaveWork()
    {
        var page = await _fixture.NewPageAsync();
        var errors = CaptureErrors(page);
        await page.GotoAsync(ProjectUrl());          // reálná předchozí stránka v historii
        await page.GotoAsync(EditorUrl());
        await WaitForTrapArmedAsync(page);
        await page.Locator("input[name='Nazev']").FillAsync("Dirty test A8");

        // GoBack s trapem nenaviguje (popstate) — nečekat na load, jen na dialog.
        try { await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit, Timeout = 3000 }); } catch { /* trap: žádná navigace */ }
        await Expect(page.Locator("[data-record-editor-close-guard]")).ToHaveCountAsync(1); // gov-dialog host má výšku 0 → „visible" nelze

        // „Pokračovat v úpravách" → dialog pryč, pořád na editoru, trap obnoven.
        await page.GetByRole(AriaRole.Button, new() { Name = "Pokračovat v úpravách" }).ClickAsync();
        await Expect(page.Locator("[data-record-editor-close-guard]")).ToHaveCountAsync(0);
        page.Url.Should().Contain("/Zaznamy/Create", $"jsErrors: {string.Join(" | ", errors)}");

        // Druhý pokus → „Zahodit změny" → odejde na předchozí stránku.
        try { await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit, Timeout = 3000 }); } catch { /* trap */ }
        await Expect(page.Locator("[data-record-editor-close-guard]")).ToHaveCountAsync(1); // gov-dialog host má výšku 0 → „visible" nelze
        await page.GetByRole(AriaRole.Button, new() { Name = "Zahodit změny" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(".*/Projekty/Detail/.*"),
            new() { Timeout = 8000 });

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task CleanEditor_BrowserBack_LeavesWithoutDialog()
    {
        var page = await _fixture.NewPageAsync();
        var errors = CaptureErrors(page);
        await page.GotoAsync(ProjectUrl());
        await page.GotoAsync(EditorUrl());
        await WaitForTrapArmedAsync(page);
        // Trap popne sentinel (bez navigace) a čistý form propustí druhým back() → skutečná navigace.
        try { await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit, Timeout = 3000 }); } catch { /* trap */ }
        // ToHaveURL (poll aktuální URL) — WaitForURL čeká na navigační EVENT, který už
        // mohl zkonzumovat GoBackAsync (real nav commitla v jeho okně).
        await Assertions.Expect(page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(".*/Projekty/Detail/.*"),
            new() { Timeout = 8000 });
        (await page.Locator("[data-record-editor-close-guard]").CountAsync()).Should().Be(0, $"jsErrors: {string.Join(" | ", errors)}");
        await page.Context.CloseAsync();
    }

    /// <summary>B2 (2026-07-09): první aktivace schedule tabu dřív PŘESTAVĚLA dirty baseline
    /// → user změny „zmizely" a dialog nevyskočil. Merge fix: absorbovat jen planner šum.</summary>
    [Fact]
    public async Task DirtyEditor_SurvivesScheduleTabVisit_BackShowsDialog()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(ProjectUrl());
        await page.GotoAsync(EditorUrl());
        await WaitForTrapArmedAsync(page);

        await page.Locator("input[name='Nazev']").FillAsync("Dirty před schedule tabem");
        // Přepnout na Harmonogram a zpět na Základní (uživatelův scénář).
        await page.Locator("pm-tabs [key='schedule']").First.ClickAsync();
        await page.WaitForTimeoutAsync(400); // planner recalc + rAF snapshot merge
        await page.Locator("pm-tabs [key='basic']").First.ClickAsync();

        try { await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit, Timeout = 3000 }); } catch { /* trap */ }
        await Expect(page.Locator("[data-record-editor-close-guard]")).ToHaveCountAsync(1); // gov-dialog host h=0
        await page.GetByRole(AriaRole.Button, new() { Name = "Pokračovat v úpravách" }).ClickAsync();
        await page.Context.CloseAsync();
    }

    /// <summary>Regrese fixu 2026-05-05: samotná návštěva schedule tabu (bez user editu)
    /// nesmí vytvořit falešný dirty — back projde bez dialogu.</summary>
    [Fact]
    public async Task CleanEditor_ScheduleTabVisitOnly_BackLeavesWithoutDialog()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(ProjectUrl());
        await page.GotoAsync(EditorUrl());
        await WaitForTrapArmedAsync(page);

        await page.Locator("pm-tabs [key='schedule']").First.ClickAsync();
        await page.WaitForTimeoutAsync(400);

        try { await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit, Timeout = 3000 }); } catch { /* trap */ }
        await Assertions.Expect(page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(".*/Projekty/Detail/.*"),
            new() { Timeout = 8000 });
        (await page.Locator("[data-record-editor-close-guard]").CountAsync()).Should().Be(0);
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task DirtyEditor_BreadcrumbBack_ShowsAppDialog()
    {
        var page = await _fixture.NewPageAsync();
        var errors = CaptureErrors(page);
        await page.GotoAsync(EditorUrl());
        await WaitForTrapArmedAsync(page);
        await page.Locator("input[name='Nazev']").FillAsync("Dirty breadcrumb A8");
        await page.Locator(".app-breadcrumb-back").ClickAsync();
        await Expect(page.Locator("[data-record-editor-close-guard]")).ToHaveCountAsync(1); // gov-dialog host má výšku 0 → „visible" nelze
        page.Url.Should().Contain("/Zaznamy/Create", $"guard musí zastavit navigaci; jsErrors: {string.Join(" | ", errors)}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Pokračovat v úpravách" }).ClickAsync();
        await page.Context.CloseAsync();
    }
}
