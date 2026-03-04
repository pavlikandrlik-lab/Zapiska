using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class HarmonogramUnifiedScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public HarmonogramUnifiedScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProjectDetail_ShouldNotRenderGanttTab_AndShouldRenderHarmonogramTab()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");

        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "GANTT" })).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Harmonogram_ShouldRenderOnlySubsystemFilter_AndPlanActualLayers()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");

        var shell = page.Locator("[data-project-filter-scope='schedule']");
        await OpenFiltersAsync(shell);

        await Expect(shell.Locator("[data-schedule-filter-key='subsystem']")).ToHaveCountAsync(1);
        await Expect(shell.Locator("[data-schedule-filter-key='mine']")).ToHaveCountAsync(0);
        await Expect(shell.Locator("[data-schedule-filter-key='vlastnik']")).ToHaveCountAsync(0);
        var scheduleCardCount = await page.Locator(".schedule-card").CountAsync();
        var emptyStateVisible = await page.GetByText("V projektu zatím nejsou úkoly s harmonogramem.").CountAsync();
        (scheduleCardCount > 0 || emptyStateVisible > 0).Should().BeTrue();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Harmonogram_ShouldRenderVisibleActualLegend_AndLayeredTracksInOverviewAndBreakdown()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");

        if (await page.Locator(".schedule-card").CountAsync() == 0)
        {
            await Expect(page.GetByText("V projektu zatím nejsou úkoly s harmonogramem.")).ToBeVisibleAsync();
            await page.Context.CloseAsync();
            return;
        }

        var firstCard = page.Locator(".schedule-card").First;
        await Expect(firstCard).ToBeVisibleAsync();

        var actualLegend = firstCard.Locator(".schedule-layered-legend").First.GetByText("Skutečnost", new() { Exact = true });
        var overviewTrack = firstCard.Locator(".schedule-layered-track--overview").First;
        await Expect(actualLegend).ToBeVisibleAsync();
        await Expect(overviewTrack).ToBeVisibleAsync();

        var actualLegendBox = await actualLegend.BoundingBoxAsync();
        var overviewTrackBox = await overviewTrack.BoundingBoxAsync();
        actualLegendBox.Should().NotBeNull();
        overviewTrackBox.Should().NotBeNull();
        (actualLegendBox!.Y + actualLegendBox.Height).Should().BeLessThan(overviewTrackBox!.Y);

        await Expect(firstCard.Locator(".schedule-rainbow-row")).ToHaveCountAsync(0);

        var overviewPlannedSegment = firstCard.Locator(".schedule-layered-track--overview .schedule-layered-segment.planned").First;
        var overviewActualSegment = firstCard.Locator(".schedule-layered-track--overview .schedule-layered-segment.actual").First;
        var overviewPlannedBox = await overviewPlannedSegment.BoundingBoxAsync();
        var overviewActualBox = await overviewActualSegment.BoundingBoxAsync();
        overviewPlannedBox.Should().NotBeNull();
        overviewActualBox.Should().NotBeNull();
        overviewPlannedBox!.Height.Should().BeGreaterThan(overviewActualBox!.Height);

        await firstCard.GetByRole(AriaRole.Button, new() { Name = "Rozpad" }).ClickAsync();

        var stepLegend = firstCard.Locator(".schedule-layered-legend--steps").GetByText("Skutečnost", new() { Exact = true });
        var stepTrack = firstCard.Locator(".schedule-layered-track--step").First;
        await Expect(stepLegend).ToBeVisibleAsync();
        await Expect(stepTrack).ToBeVisibleAsync();

        var stepLegendBox = await stepLegend.BoundingBoxAsync();
        var stepTrackBox = await stepTrack.BoundingBoxAsync();
        stepLegendBox.Should().NotBeNull();
        stepTrackBox.Should().NotBeNull();
        (stepLegendBox!.Y + stepLegendBox.Height).Should().BeLessThan(stepTrackBox!.Y);

        var stepPlannedSegment = firstCard.Locator(".schedule-layered-track--step .schedule-layered-segment.planned").First;
        var stepActualSegment = firstCard.Locator(".schedule-layered-track--step .schedule-layered-segment.actual").First;
        var stepPlannedBox = await stepPlannedSegment.BoundingBoxAsync();
        var stepActualBox = await stepActualSegment.BoundingBoxAsync();
        stepPlannedBox.Should().NotBeNull();
        stepActualBox.Should().NotBeNull();
        stepPlannedBox!.Height.Should().BeGreaterThan(stepActualBox!.Height);

        await page.Context.CloseAsync();
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

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
