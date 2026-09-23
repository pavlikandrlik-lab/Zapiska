using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class ProjectFilterPreferencesScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public ProjectFilterPreferencesScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProjectRecordsFilters_ShouldRenderActiveChips_AndRemoveChip()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync(ProjectDetailUrl("zaznamy"));

        var recordsShell = page.Locator("[data-project-filter-scope='records']");
        await OpenFiltersAsync(recordsShell);
        await Expect(recordsShell.Locator("[data-filter-key='aktivni']")).ToBeCheckedAsync();

        var chipRow = page.Locator("[data-filter-chip-row='records']");
        await Expect(chipRow).ToContainTextAsync("Pouze aktivní");

        await chipRow.Locator("[data-filter-chip-remove='records']").ClickAsync();

        await Expect(chipRow).ToBeHiddenAsync();
        await Expect(recordsShell.Locator("[data-filter-key='aktivni']")).Not.ToBeCheckedAsync();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ProjectDetailTabs_ShouldNavigateAndRenderServerSidePanels()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync(ProjectDetailUrl("zaznamy"));

        await page.GotoAsync(ProjectDetailUrl("harmonogram"));
        await Expect(page.Locator("[data-tab-panel='harmonogram']")).ToBeVisibleAsync();

        await page.GotoAsync(ProjectDetailUrl("jednani"));
        await Expect(page.Locator("[data-tab-panel='jednani']")).ToBeVisibleAsync();

        await page.GotoAsync(ProjectDetailUrl("tym"));
        await Expect(page.Locator("[data-tab-panel='tym']")).ToBeVisibleAsync();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ProjectFilters_ShouldSaveDefaults_ForAllPanels_AndRestoreAfterReload()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync(ProjectDetailUrl("zaznamy"));

        var recordsShell = page.Locator("[data-project-filter-scope='records']");
        await OpenFiltersAsync(recordsShell);
        // „Jen mé záznamy" zrušen 2026-07-05 — persistenci ověřujeme na switchi „Pouze aktivní záznamy"
        // (výchozí zapnutý → vypneme, aby uložená hodnota byla non-default).
        await ToggleSwitchAsync(recordsShell, "Pouze aktivní záznamy");
        await recordsShell.Locator("[data-filter-save-defaults='records']").ClickAsync();
        await Expect(recordsShell.Locator("[data-filter-save-status='records']")).ToContainTextAsync("Výchozí filtry uloženy");

        await page.Locator("[data-tab='harmonogram']").ClickAsync();
        var scheduleShell = page.Locator("[data-project-filter-scope='schedule']");
        await OpenFiltersAsync(scheduleShell);
        var selectedSubsystem = await scheduleShell.Locator("[data-schedule-filter-key='subsystem']").InputValueAsync();
        await scheduleShell.Locator("[data-filter-save-defaults='schedule']").ClickAsync();
        await Expect(scheduleShell.Locator("[data-filter-save-status='schedule']")).ToContainTextAsync("Výchozí filtry uloženy");

        await page.ReloadAsync();

        await page.Locator("[data-tab='zaznamy']").ClickAsync();
        await OpenFiltersAsync(page.Locator("[data-project-filter-scope='records']"));
        await Expect(page.Locator("[data-project-filter-scope='records'] [data-filter-key='aktivni']")).Not.ToBeCheckedAsync();

        await page.Locator("[data-tab='harmonogram']").ClickAsync();
        var restoredScheduleShell = page.Locator("[data-project-filter-scope='schedule']");
        await OpenFiltersAsync(restoredScheduleShell);
        await Expect(restoredScheduleShell.Locator("[data-schedule-filter-key='subsystem']")).ToHaveValueAsync(selectedSubsystem);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Profile_ShouldRemoveSingleProjectFilterPreference()
    {
        var page = await _fixture.NewPageAsync();

        // Ulož výchozí filtry pro aktuální projekt (vznikne jedna položka předvolby).
        await page.GotoAsync(ProjectDetailUrl("zaznamy"));
        var recordsShell = page.Locator("[data-project-filter-scope='records']");
        await OpenFiltersAsync(recordsShell);
        await ToggleSwitchAsync(recordsShell, "Pouze aktivní záznamy");
        await recordsShell.Locator("[data-filter-save-defaults='records']").ClickAsync();
        await Expect(recordsShell.Locator("[data-filter-save-status='records']")).ToContainTextAsync("Výchozí filtry uloženy");

        // Profil ▸ Předvolby — řádek filtrů projektu má vlastní tlačítko smazat.
        await page.GotoAsync($"{_fixture.BaseUrl}/Profil?asUser={_fixture.AdminOsobaId}");
        var list = page.Locator("[data-preferences-list]");
        var projectFilterRemove = list.Locator("[data-preference-remove][data-preference-descriptor='projectFilters']");
        await Expect(projectFilterRemove).ToHaveCountAsync(1);

        await projectFilterRemove.First.ClickAsync();
        await Expect(page.Locator("[data-preferences-status]")).ToContainTextAsync("odstraněna");
        await Expect(projectFilterRemove).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ProjectTeamTab_ShouldApplySharedSearch_AfterLazyLoad()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync(ProjectDetailUrl("zaznamy"));
        await page.Locator("[data-tab='tym']").ClickAsync();

        var teamPanel = page.Locator("[data-tab-panel='tym']");
        await Expect(teamPanel.Locator("[data-table-tools-search-input]")).ToBeVisibleAsync();

        await teamPanel.Locator("[data-table-tools-search-input]").FillAsync("__no_match__");
        await Expect(teamPanel.Locator("[data-table-empty-row]").First).ToBeVisibleAsync();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ProjectPrint_ShouldPromptForFilterChoice_WhenRelevantFiltersAreActive_AndUsePreferredFormat()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(ProjectDetailUrl("zaznamy"));

        await page.EvaluateAsync("window.localStorage.setItem('pmtracker.print.preferredFormat', 'pdf');");

        var popup = await page.RunAndWaitForPopupAsync(async () =>
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Tisk projektu" }).ClickAsync();
            await Expect(page.Locator("[data-print-filter-scope='current']")).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Použít aktuální filtry" }).ClickAsync();
        });

        popup.Url.Should().Contain($"/Export/Projekt/{_fixture.ProjectId}/Tisk");
        popup.Url.Should().Contain("useCurrentFilters=true");
        popup.Url.Should().Contain("aktivni=true");
        popup.Url.Should().NotContain("groupBySubsystem");

        await popup.CloseAsync();
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ProjectPrint_ShouldShowFormatChooserAfterFilterChoice_WhenNoPreferredFormatExists()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(ProjectDetailUrl("zaznamy"));

        await page.EvaluateAsync("window.localStorage.removeItem('pmtracker.print.preferredFormat');");

        await page.GetByRole(AriaRole.Button, new() { Name = "Tisk projektu" }).ClickAsync();
        await Expect(page.Locator("[data-print-filter-scope='current']")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Použít aktuální filtry" }).ClickAsync();

        await Expect(page.Locator("[data-print-choice='pdf']")).ToBeVisibleAsync();
        await Expect(page.Locator("[data-print-choice='word']")).ToBeVisibleAsync();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ProjectPrint_ShouldSkipFilterPrompt_WhenNoRelevantFiltersAreActive()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(ProjectDetailUrl("zaznamy"));

        await page.EvaluateAsync("window.localStorage.removeItem('pmtracker.print.preferredFormat');");

        var recordsShell = page.Locator("[data-project-filter-scope='records']");
        await OpenFiltersAsync(recordsShell);
        await recordsShell.Locator("[data-filter-key='aktivni']").UncheckAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Tisk projektu" }).ClickAsync();

        await Expect(page.Locator("[data-print-filter-scope='current']")).ToHaveCountAsync(0);
        await Expect(page.Locator("[data-print-choice='pdf']")).ToBeVisibleAsync();
        await Expect(page.Locator("[data-print-choice='word']")).ToBeVisibleAsync();

        await page.Context.CloseAsync();
    }

    private string ProjectDetailUrl(string tab)
    {
        return $"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab={tab}&asUser={_fixture.AdminOsobaId}";
    }

    private static async Task OpenFiltersAsync(ILocator shell)
    {
        var toggle = shell.GetByRole(AriaRole.Button, new() { Name = "Filtry" });
        var panel = shell.Locator(".filter-panel");
        if (await panel.IsHiddenAsync())
        {
            await toggle.ClickAsync();
        }
    }

    private static async Task ToggleSwitchAsync(ILocator shell, string label)
    {
        await shell.Locator("label.gov-switch")
            .Filter(new LocatorFilterOptions { HasTextString = label })
            .ClickAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
