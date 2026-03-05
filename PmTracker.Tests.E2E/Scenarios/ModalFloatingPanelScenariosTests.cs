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
    public async Task RecordEditorOwnerPicker_ShouldAvoidActionBar_AndKeepSideStableWhileTyping()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Nový záznam" }).ClickAsync();

        var modal = page.Locator(".modal-overlay");
        await Expect(modal.Locator("form[data-record-editor-form='true']")).ToBeVisibleAsync();

        var content = modal.Locator(".modal-content");
        await content.EvaluateAsync(
            """
            (node) => {
                if (!(node instanceof HTMLElement)) {
                    return;
                }

                node.scrollTop = node.scrollHeight;
            }
            """);

        var ownerInput = modal.Locator("[data-record-owner-picker] [data-person-picker-input]");
        await ownerInput.FillAsync("a");

        var panel = modal.Locator("[data-modal-floating-root] [data-person-picker-panel]");
        await Expect(panel).ToBeVisibleAsync();

        var actionBar = modal.Locator(".record-editor-actions");
        await Expect(actionBar).ToBeVisibleAsync();

        var firstState = await ReadPickerStateAsync(page, panel, ownerInput, actionBar);

        var firstLabel = await panel.Locator(".office-search-item .office-search-primary").First.InnerTextAsync();
        await ownerInput.FillAsync(firstLabel);
        await page.WaitForTimeoutAsync(320);
        await Expect(panel).ToBeVisibleAsync();

        var secondState = await ReadPickerStateAsync(page, panel, ownerInput, actionBar);

        firstState.Side.Should().Be(secondState.Side, "panel nesmí při změně počtu výsledků přeskakovat nad/pod input");
        firstState.IntersectsActionBar.Should().BeFalse();
        secondState.IntersectsActionBar.Should().BeFalse();

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

    private static async Task<(string Side, bool IntersectsActionBar)> ReadPickerStateAsync(
        IPage page,
        ILocator panel,
        ILocator input,
        ILocator actionBar)
    {
        var panelBox = await panel.BoundingBoxAsync();
        var inputBox = await input.BoundingBoxAsync();
        var actionBarBox = await actionBar.BoundingBoxAsync();

        panelBox.Should().NotBeNull();
        inputBox.Should().NotBeNull();
        actionBarBox.Should().NotBeNull();

        var side = panelBox!.Y + panelBox.Height <= inputBox!.Y + 1 ? "above" : "below";
        var intersectsActionBar = panelBox.Y < actionBarBox!.Y + actionBarBox.Height
            && panelBox.Y + panelBox.Height > actionBarBox.Y;

        var viewportHeight = await page.EvaluateAsync<int>("() => window.innerHeight");
        (panelBox.Y >= 0).Should().BeTrue();
        (panelBox.Y + panelBox.Height <= viewportHeight + 1).Should().BeTrue();

        return (side, intersectsActionBar);
    }
}
