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

        await recordsShell.GetByLabel("Pouze aktivní úkoly").CheckAsync();

        var chipRow = page.Locator("[data-filter-chip-row='records']");
        await Expect(chipRow).ToContainTextAsync("Pouze aktivní");

        await chipRow.Locator("[data-filter-chip-remove='records']").ClickAsync();

        await Expect(chipRow).ToBeHiddenAsync();
        await Expect(recordsShell.Locator("[data-filter-key='aktivni']")).Not.ToBeCheckedAsync();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ProjectFilters_ShouldSaveDefaults_ForAllPanels_AndRestoreAfterReload()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync(ProjectDetailUrl("zaznamy"));

        var recordsShell = page.Locator("[data-project-filter-scope='records']");
        await OpenFiltersAsync(recordsShell);
        await ToggleSwitchAsync(recordsShell, "Jen mé záznamy");
        await recordsShell.Locator("[data-filter-save-defaults='records']").ClickAsync();
        await Expect(recordsShell.Locator("[data-filter-save-status='records']")).ToContainTextAsync("Výchozí filtry uloženy");

        await page.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();
        var scheduleShell = page.Locator("[data-project-filter-scope='schedule']");
        await OpenFiltersAsync(scheduleShell);
        await ToggleSwitchAsync(scheduleShell, "Jen mé úkoly");
        await scheduleShell.Locator("[data-filter-save-defaults='schedule']").ClickAsync();
        await Expect(scheduleShell.Locator("[data-filter-save-status='schedule']")).ToContainTextAsync("Výchozí filtry uloženy");

        await page.GetByRole(AriaRole.Button, new() { Name = "GANTT" }).ClickAsync();
        var ganttShell = page.Locator("[data-project-filter-scope='gantt']");
        await OpenFiltersAsync(ganttShell);
        await ToggleSwitchAsync(ganttShell, "Jen mé úkoly");
        await ganttShell.Locator("[data-filter-save-defaults='gantt']").ClickAsync();
        await Expect(ganttShell.Locator("[data-filter-save-status='gantt']")).ToContainTextAsync("Výchozí filtry uloženy");

        await page.ReloadAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Záznamy" }).ClickAsync();
        await OpenFiltersAsync(page.Locator("[data-project-filter-scope='records']"));
        await Expect(page.Locator("[data-project-filter-scope='records'] [data-filter-key='mine']")).ToBeCheckedAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();
        await OpenFiltersAsync(page.Locator("[data-project-filter-scope='schedule']"));
        await Expect(page.Locator("[data-project-filter-scope='schedule'] [data-schedule-filter-key='mine']")).ToBeCheckedAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "GANTT" }).ClickAsync();
        await OpenFiltersAsync(page.Locator("[data-project-filter-scope='gantt']"));
        await Expect(page.Locator("[data-project-filter-scope='gantt'] [data-gantt-filter-key='mine']")).ToBeCheckedAsync();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Profile_ShouldClearStoredProjectFilterPreferences()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync(ProjectDetailUrl("zaznamy"));

        var recordsShell = page.Locator("[data-project-filter-scope='records']");
        await OpenFiltersAsync(recordsShell);
        await ToggleSwitchAsync(recordsShell, "Jen mé záznamy");
        await recordsShell.Locator("[data-filter-save-defaults='records']").ClickAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Profil?asUser={_fixture.AdminOsobaId}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Smazat uložené projektové filtry" }).ClickAsync();
        await Expect(page.Locator("[data-project-filter-preferences-status]")).ToContainTextAsync("Uložené projektové filtry byly odstraněny.");

        await page.GotoAsync(ProjectDetailUrl("zaznamy"));
        recordsShell = page.Locator("[data-project-filter-scope='records']");
        await OpenFiltersAsync(recordsShell);

        await Expect(recordsShell.Locator("[data-filter-key='mine']")).Not.ToBeCheckedAsync();
        await Expect(recordsShell.Locator("[data-filter-key='groupBySubsystem']")).ToBeCheckedAsync();

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
