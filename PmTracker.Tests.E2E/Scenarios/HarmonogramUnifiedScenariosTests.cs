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
        await Expect(shell.Locator("[data-schedule-filter-key='subsystem']")).ToHaveCountAsync(1);
        await Expect(shell.Locator("[data-schedule-filter-key='mine']")).ToHaveCountAsync(0);
        await Expect(shell.Locator("[data-schedule-filter-key='vlastnik']")).ToHaveCountAsync(0);
        await Expect(page.Locator("[data-project-schedule-list]")).ToHaveCountAsync(1);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Harmonogram_ShouldRenderVisibleActualLegend_AndLayeredTracksInOverviewAndBreakdown()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");

        if (await page.Locator(".schedule-card").CountAsync() == 0)
        {
            await Expect(page.Locator("[data-project-schedule-list]")).ToHaveCountAsync(1);
            await page.Context.CloseAsync();
            return;
        }

        var firstCard = page.Locator(".schedule-card").First;
        await Expect(firstCard.Locator(".schedule-overview-row")).ToHaveCountAsync(2);
        await Expect(firstCard.Locator(".schedule-overview-axis")).ToHaveCountAsync(1);
        await Expect(firstCard.Locator(".schedule-overview-marker.today")).ToHaveCountAsync(1);
        await Expect(firstCard.Locator(".schedule-overview-marker.deadline")).ToHaveCountAsync(1);
        await Expect(page.Locator(".tab-panel[data-tab-panel='harmonogram'] .schedule-mini-legend")).ToHaveCountAsync(0);
        await Expect(firstCard.Locator(".schedule-layered-legend--steps")).ToHaveCountAsync(1);
        await Expect(firstCard.Locator(".schedule-layered-axis")).ToHaveCountAsync(1);
        (await firstCard.Locator(".schedule-layered-track--step").CountAsync()).Should().BeGreaterThan(0);
        (await firstCard.Locator(".schedule-layered-marker.today").CountAsync()).Should().BeGreaterThan(0);
        await Expect(firstCard.Locator(".schedule-layered-marker.deadline")).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
