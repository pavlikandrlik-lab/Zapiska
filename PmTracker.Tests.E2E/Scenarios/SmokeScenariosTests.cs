using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class SmokeScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public SmokeScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SettingsActionsSection_ShouldOpenCreatePermissionModal()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Nastaveni?section=akce&asUser={_fixture.AdminOsobaId}");

        await page.GetByRole(AriaRole.Button, new() { Name = "Nová akce" }).ClickAsync();

        var modalTitle = page.GetByRole(AriaRole.Heading, new() { Name = "Nová akce" });
        await Expect(modalTitle).ToBeVisibleAsync();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ProjectMeetingTab_ShouldOpenNewMeetingModal()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=jednani&asUser={_fixture.AdminOsobaId}");

        await page.GetByRole(AriaRole.Button, new() { Name = "Nové jednání" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Nové jednání" })).ToBeVisibleAsync();

        await page.Context.CloseAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
