using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class ModalFloatingPanelScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public ModalFloatingPanelScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ModalPersonPicker_ShouldNotBeClipped_WhenAnchorIsNearBottom()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=tym&asUser={_fixture.AdminOsobaId}");

        await page.GetByRole(AriaRole.Button, new() { Name = "Přidat projektovou roli" }).ClickAsync();

        var modal = page.Locator(".modal-overlay");
        await Expect(modal.GetByRole(AriaRole.Heading, new() { Name = "Přidat projektovou roli" })).ToBeVisibleAsync();

        await modal.Locator("[data-modal-container]").EvaluateAsync(
            """
            (container) => {
                if (!(container instanceof HTMLElement)) {
                    return;
                }

                container.style.transform = "translateY(180px)";
            }
            """);

        var ownerInput = modal.Locator("[data-person-picker-input]");
        await ownerInput.FocusAsync();

        var panel = modal.Locator("[data-modal-floating-root] [data-person-picker-panel]");
        await Expect(panel).ToBeVisibleAsync();

        var panelBox = await panel.BoundingBoxAsync();
        panelBox.Should().NotBeNull();

        var viewportHeight = await page.EvaluateAsync<int>("() => window.innerHeight");
        (panelBox!.Y >= 0).Should().BeTrue();
        (panelBox.Y + panelBox.Height <= viewportHeight + 1).Should().BeTrue();

        var options = panel.Locator(".office-search-item");
        var optionCount = await options.CountAsync();
        optionCount.Should().BeGreaterThan(0);

        if (optionCount > 1)
        {
            await Expect(options.Nth(1)).ToBeVisibleAsync();
        }

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ModalDatePicker_ShouldEscapeModalContentClipping()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=jednani&asUser={_fixture.AdminOsobaId}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Nová porada" }).ClickAsync();

        var modal = page.Locator(".modal-overlay");
        var dateDisplay = modal.Locator("[data-app-date-field] [data-app-date-display]").First;
        await dateDisplay.ClickAsync();

        var panel = modal.Locator("[data-modal-floating-root] [data-app-date-panel]");
        await Expect(panel).ToBeVisibleAsync();

        var panelBox = await panel.BoundingBoxAsync();
        panelBox.Should().NotBeNull();

        var viewportHeight = await page.EvaluateAsync<int>("() => window.innerHeight");
        (panelBox!.Y >= 0).Should().BeTrue();
        (panelBox.Y + panelBox.Height <= viewportHeight + 1).Should().BeTrue();

        await Expect(panel.Locator("[data-app-date-grid] .app-date-day").Nth(20)).ToBeVisibleAsync();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ClosingModal_ShouldCleanupFloatingPanels()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=jednani&asUser={_fixture.AdminOsobaId}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Nová porada" }).ClickAsync();

        var modal = page.Locator(".modal-overlay");
        await modal.Locator("[data-app-date-field] [data-app-date-display]").First.ClickAsync();
        await Expect(modal.Locator("[data-modal-floating-root] [data-app-date-panel]")).ToBeVisibleAsync();

        await modal.GetByRole(AriaRole.Button, new() { Name = "Zavřít dialog" }).ClickAsync();
        await Expect(page.Locator(".modal-overlay")).ToHaveCountAsync(0);

        var floatingPanelCount = await page.EvaluateAsync<int>(
            "() => document.querySelectorAll('[data-modal-floating-root] [data-floating-panel], .app-floating-root [data-floating-panel]').length");
        floatingPanelCount.Should().Be(0);

        await page.Context.CloseAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
