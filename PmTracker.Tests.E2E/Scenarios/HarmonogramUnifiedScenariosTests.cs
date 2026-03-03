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
