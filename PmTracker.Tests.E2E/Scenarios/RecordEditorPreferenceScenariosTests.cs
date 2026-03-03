using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class RecordEditorPreferenceScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public RecordEditorPreferenceScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NewRecordChooser_ShouldOpenOnlyWhenNoStoredPreference()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync(ProjectDetailUrl("zaznamy"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Nový záznam" }).ClickAsync();
        await Expect(page.Locator("[data-record-editor-popover]")).ToBeVisibleAsync();

        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Nový záznam" }).ClickAsync();
        await Expect(page.Locator("[data-record-editor-popover]")).ToHaveCountAsync(0);
        await Expect(page.Locator(".modal-overlay")).ToHaveCountAsync(1);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task NewRecordChooser_ShouldStoreChoiceByDefault_AndProfileShouldClearIt()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync(ProjectDetailUrl("zaznamy"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Nový záznam" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Otevřít v modalu" }).ClickAsync();

        var storedPreference = await page.EvaluateAsync<string?>("() => localStorage.getItem('pmtracker.recordEditor.preference')");
        storedPreference.Should().Be("modal");

        await page.GotoAsync($"{_fixture.BaseUrl}/Profil?asUser={_fixture.AdminOsobaId}");
        await Expect(page.Locator("[data-record-editor-preference-current]")).ToContainTextAsync("Otevřít v modalu");
        await page.Locator("[data-record-editor-preference-reset]").ClickAsync();
        await Expect(page.Locator("[data-record-editor-preference-status]")).ToContainTextAsync("Uložená výchozí volba byla odstraněna.");

        await page.GotoAsync(ProjectDetailUrl("zaznamy"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Nový záznam" }).ClickAsync();
        await Expect(page.Locator("[data-record-editor-popover]")).ToBeVisibleAsync();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task NewRecordChooser_ShouldNotStoreChoice_WhenNegativeCheckboxIsChecked()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync(ProjectDetailUrl("zaznamy"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Nový záznam" }).ClickAsync();

        var chooser = page.Locator("[data-record-editor-popover]");
        await chooser.GetByLabel("Neukládat pro tentokrát jako výchozí volbu").CheckAsync();
        await chooser.GetByRole(AriaRole.Button, new() { Name = "Otevřít v modalu" }).ClickAsync();

        var storedPreference = await page.EvaluateAsync<string?>("() => localStorage.getItem('pmtracker.recordEditor.preference')");
        storedPreference.Should().BeNull();

        await page.Context.CloseAsync();
    }

    private string ProjectDetailUrl(string tab)
    {
        return $"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab={tab}&asUser={_fixture.AdminOsobaId}";
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
