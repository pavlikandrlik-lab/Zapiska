using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class MeetingDateTimePickerScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public MeetingDateTimePickerScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MeetingDateAndTimePickers_ShouldHaveSameWidth_AndWhiteSurfaceInLightTheme()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=jednani&asUser={_fixture.AdminOsobaId}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Nové jednání" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Nové jednání" })).ToBeVisibleAsync();

        var modal = page.Locator(".modal-overlay");
        var dateField = modal.Locator("[data-app-date-field]").First;
        var dateDisplay = dateField.Locator("[data-app-date-display]");
        var dateTrigger = dateField.Locator("[data-app-date-open]");
        var datePanel = modal.Locator("[data-app-date-panel]");
        await dateDisplay.ClickAsync();
        await Expect(datePanel).ToBeVisibleAsync();

        var dateBox = await datePanel.BoundingBoxAsync();
        dateBox.Should().NotBeNull();

        var dateBackground = await datePanel.EvaluateAsync<string>(
            "element => window.getComputedStyle(element).backgroundColor");
        dateBackground.Should().Be("rgb(255, 255, 255)");

        await dateTrigger.ClickAsync();
        await Expect(datePanel).ToBeHiddenAsync();

        var timeField = modal.Locator("[data-app-time-field]").First;
        var timeDisplay = timeField.Locator("[data-app-time-display]");
        var timePanel = modal.Locator("[data-app-time-panel]");
        await timeDisplay.ClickAsync();
        await Expect(timePanel).ToBeVisibleAsync();

        var timeBox = await timePanel.BoundingBoxAsync();
        timeBox.Should().NotBeNull();

        Math.Abs((dateBox?.Width ?? 0) - (timeBox?.Width ?? 0)).Should().BeLessThanOrEqualTo(1);

        var timeBackground = await timePanel.EvaluateAsync<string>(
            "element => window.getComputedStyle(element).backgroundColor");
        timeBackground.Should().Be("rgb(255, 255, 255)");

        var timeOption = timePanel.Locator("[data-app-time-grid] .app-time-option").Nth(5);
        await Expect(timeOption).ToBeVisibleAsync();
        await timeOption.ClickAsync();
        await Expect(timePanel).ToBeHiddenAsync();
        await Expect(timeField.Locator("[data-app-time-display]")).ToHaveValueAsync("01:15");

        await page.Context.CloseAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
