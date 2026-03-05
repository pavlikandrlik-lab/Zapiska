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
    public async Task HarmonogramAxis_ShouldRenderDates_AfterSwitchingFromAnotherTab()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?asUser={_fixture.AdminOsobaId}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();

        if (await page.Locator(".schedule-card").CountAsync() == 0)
        {
            await Expect(page.Locator("[data-project-schedule-list]")).ToHaveCountAsync(1);
            await page.Context.CloseAsync();
            return;
        }

        var firstCard = page.Locator(".schedule-card").First;
        var labels = firstCard.Locator(".schedule-overview-axis .timeline-axis-label:not([hidden])");
        await Expect(labels.First).ToBeVisibleAsync();
        (await labels.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
        await Expect(firstCard.Locator(".schedule-overview-axis .timeline-axis-marker.today")).ToHaveCountAsync(1);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task RecordEditorScheduleTab_ShouldRenderSingleMiniGanttAxis_WithVisibleDateLabels()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();

        if (await page.Locator(".schedule-card").CountAsync() == 0)
        {
            await Expect(page.Locator("[data-project-schedule-list]")).ToHaveCountAsync(1);
            await page.Context.CloseAsync();
            return;
        }

        await page.Locator(".schedule-card .btn.small", new() { HasTextString = "Upravit" }).First.ClickAsync();

        var modal = page.Locator(".modal-overlay");
        var form = modal.Locator("form[data-record-editor-form='true']");
        await Expect(form).ToBeVisibleAsync();

        await modal.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();

        var axes = form.Locator("[data-schedule-axis]");
        await Expect(axes).ToHaveCountAsync(1);

        var labels = axes.First.Locator(".timeline-axis-label:not([hidden])");
        await Expect(labels.First).ToBeVisibleAsync();

        (await labels.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
        ((await labels.First.InnerTextAsync()) ?? string.Empty).Trim().Should().NotBeEmpty();
        await Expect(axes.First.Locator(".timeline-axis-marker.today")).ToHaveCountAsync(1);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task RecordEditorScheduleTab_ShouldRenderPlanAndActualMiniGanttAsTwoSeparateRows()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();

        if (await page.Locator(".schedule-card").CountAsync() == 0)
        {
            await Expect(page.Locator("[data-project-schedule-list]")).ToHaveCountAsync(1);
            await page.Context.CloseAsync();
            return;
        }

        await page.Locator(".schedule-card .btn.small", new() { HasTextString = "Upravit" }).First.ClickAsync();

        var modal = page.Locator(".modal-overlay");
        var form = modal.Locator("form[data-record-editor-form='true']");
        await Expect(form).ToBeVisibleAsync();
        await modal.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();

        var rows = form.Locator("[data-record-mini-gantt] .schedule-mini-gantt-row");
        await Expect(rows).ToHaveCountAsync(2);
        await Expect(rows.Nth(0).Locator(".schedule-mini-gantt-row-label")).ToContainTextAsync("Plán");
        await Expect(rows.Nth(1).Locator(".schedule-mini-gantt-row-label")).ToContainTextAsync("Skutečnost");

        var firstRowTrack = rows.Nth(0).Locator(".schedule-mini-gantt-track");
        var secondRowTrack = rows.Nth(1).Locator(".schedule-mini-gantt-track");
        await Expect(firstRowTrack).ToBeVisibleAsync();
        await Expect(secondRowTrack).ToBeVisibleAsync();

        var firstRowBox = await rows.Nth(0).BoundingBoxAsync();
        var secondRowBox = await rows.Nth(1).BoundingBoxAsync();
        firstRowBox.Should().NotBeNull();
        secondRowBox.Should().NotBeNull();
        secondRowBox!.Y.Should().BeGreaterThan(firstRowBox!.Y);

        var firstTrackBox = await firstRowTrack.BoundingBoxAsync();
        var secondTrackBox = await secondRowTrack.BoundingBoxAsync();
        firstTrackBox.Should().NotBeNull();
        secondTrackBox.Should().NotBeNull();
        firstTrackBox!.Width.Should().BeGreaterThan(40);
        secondTrackBox!.Width.Should().BeGreaterThan(40);

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
        await Expect(firstCard.Locator(".schedule-overview-axis .timeline-axis-marker.today")).ToHaveCountAsync(1);
        (await firstCard.Locator(".schedule-layered-track--step").CountAsync()).Should().BeGreaterThan(0);
        (await firstCard.Locator(".schedule-layered-marker.today").CountAsync()).Should().BeGreaterThan(0);
        await Expect(firstCard.Locator(".schedule-layered-marker.deadline")).ToHaveCountAsync(0);

        var overviewLabels = firstCard.Locator(".schedule-overview-axis .timeline-axis-label:not([hidden])");
        var overviewLabelCount = await overviewLabels.CountAsync();
        overviewLabelCount.Should().BeGreaterThanOrEqualTo(5);
        overviewLabelCount.Should().BeLessThanOrEqualTo(10);
        ((await overviewLabels.First.InnerTextAsync()) ?? string.Empty).Trim().Should().NotBeEmpty();
        ((await overviewLabels.Last.InnerTextAsync()) ?? string.Empty).Trim().Should().NotBeEmpty();

        var expandToggle = firstCard.Locator("[data-schedule-expand-toggle]");
        if (await expandToggle.CountAsync() > 0)
        {
            var expanded = await expandToggle.First.GetAttributeAsync("aria-expanded");
            if (expanded is null || !expanded.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                await expandToggle.First.ClickAsync();
            }
        }

        await page.WaitForTimeoutAsync(120);
        var layeredLabels = firstCard.Locator(".schedule-layered-axis .timeline-axis-label:not([hidden])");
        var layeredLabelCount = await layeredLabels.CountAsync();
        layeredLabelCount.Should().BeGreaterThanOrEqualTo(5);
        layeredLabelCount.Should().BeLessThanOrEqualTo(10);
        ((await layeredLabels.First.InnerTextAsync()) ?? string.Empty).Trim().Should().NotBeEmpty();
        ((await layeredLabels.Last.InnerTextAsync()) ?? string.Empty).Trim().Should().NotBeEmpty();
        await Expect(firstCard.Locator(".schedule-layered-axis .timeline-axis-marker.today")).ToHaveCountAsync(1);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task HarmonogramAxes_ShouldKeepDateLabelsVisible_AfterRepeatedTabSwitches()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?asUser={_fixture.AdminOsobaId}");

        for (var attempt = 0; attempt < 3; attempt += 1)
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();
            if (await page.Locator(".schedule-card").CountAsync() > 0)
            {
                var firstCard = page.Locator(".schedule-card").First;
                var labels = firstCard.Locator(".schedule-overview-axis .timeline-axis-label:not([hidden])");
                await Expect(labels.First).ToBeVisibleAsync();
                (await labels.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
                await Expect(firstCard.Locator(".schedule-overview-axis .timeline-axis-marker.today")).ToHaveCountAsync(1);
            }

            await page.GetByRole(AriaRole.Button, new() { Name = "Záznamy" }).ClickAsync();
        }

        await page.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();
        if (await page.Locator(".schedule-card").CountAsync() == 0)
        {
            await Expect(page.Locator("[data-project-schedule-list]")).ToHaveCountAsync(1);
            await page.Context.CloseAsync();
            return;
        }

        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();

        if (await page.Locator(".schedule-card").CountAsync() == 0)
        {
            await Expect(page.Locator("[data-project-schedule-list]")).ToHaveCountAsync(1);
            await page.Context.CloseAsync();
            return;
        }

        await page.Locator(".schedule-card .btn.small", new() { HasTextString = "Upravit" }).First.ClickAsync();
        var modal = page.Locator(".modal-overlay");
        var form = modal.Locator("form[data-record-editor-form='true']");
        await Expect(form).ToBeVisibleAsync();

        for (var attempt = 0; attempt < 3; attempt += 1)
        {
            await modal.GetByRole(AriaRole.Button, new() { Name = "Základní" }).ClickAsync();
            await modal.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();

            var axis = form.Locator("[data-schedule-axis]").First;
            var labels = axis.Locator(".timeline-axis-label:not([hidden])");
            await Expect(labels.First).ToBeVisibleAsync();
            (await labels.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
            await Expect(axis.Locator(".timeline-axis-marker.today")).ToHaveCountAsync(1);
        }

        await page.Context.CloseAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
