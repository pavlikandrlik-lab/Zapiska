using FluentAssertions;
using Microsoft.Data.SqlClient;
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

        await Expect(page.Locator("[data-tab='harmonogram']")).ToBeVisibleAsync();
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
        await page.Locator("[data-tab='harmonogram']").ClickAsync();

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
        await Expect(form.Locator(".schedule-mini-gantt-marker.today")).ToHaveCountAsync(2);

        (await labels.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
        ((await labels.First.InnerTextAsync()) ?? string.Empty).Trim().Should().NotBeEmpty();
        await AssertAxisEdgeLabelsAlignedWithinBoundsAsync(axes.First);

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
        await Expect(form.Locator(".schedule-mini-gantt-marker.today")).ToHaveCountAsync(2);

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
        (await firstCard.Locator(".schedule-layered-track--step").CountAsync()).Should().BeGreaterThan(0);
        (await firstCard.Locator(".schedule-layered-marker.today").CountAsync()).Should().BeGreaterThan(0);
        await Expect(firstCard.Locator(".schedule-layered-marker.deadline")).ToHaveCountAsync(0);

        var overviewTodayColor = await firstCard.Locator(".schedule-overview-marker.today").First.EvaluateAsync<string>(
            "node => getComputedStyle(node).backgroundColor");
        overviewTodayColor.Should().Be("rgb(17, 17, 17)");

        var overviewLabels = firstCard.Locator(".schedule-overview-axis .timeline-axis-label:not([hidden])");
        var overviewLabelCount = await overviewLabels.CountAsync();
        overviewLabelCount.Should().BeGreaterThanOrEqualTo(5);
        overviewLabelCount.Should().BeLessThanOrEqualTo(10);
        ((await overviewLabels.First.InnerTextAsync()) ?? string.Empty).Trim().Should().NotBeEmpty();
        ((await overviewLabels.Last.InnerTextAsync()) ?? string.Empty).Trim().Should().NotBeEmpty();
        await AssertAxisEdgeLabelsAlignedWithinBoundsAsync(firstCard.Locator(".schedule-overview-axis").First);

        var expandToggle = firstCard.Locator("[data-schedule-expand-toggle]");
        if (await expandToggle.CountAsync() > 0)
        {
            var breakdown = firstCard.Locator("[data-schedule-steps]");
            await Expect(breakdown).ToBeHiddenAsync();
            var expanded = await expandToggle.First.GetAttributeAsync("aria-expanded");
            if (expanded is null || !expanded.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                await expandToggle.First.ClickAsync();
            }

            await Expect(breakdown).ToBeVisibleAsync();
            (await expandToggle.First.GetAttributeAsync("aria-expanded")).Should().Be("true");

            await expandToggle.First.ClickAsync();
            await Expect(breakdown).ToBeHiddenAsync();
            (await expandToggle.First.GetAttributeAsync("aria-expanded")).Should().Be("false");

            await expandToggle.First.ClickAsync();
            await Expect(breakdown).ToBeVisibleAsync();
            (await expandToggle.First.GetAttributeAsync("aria-expanded")).Should().Be("true");
        }

        await page.WaitForTimeoutAsync(120);
        var layeredLabels = firstCard.Locator(".schedule-layered-axis .timeline-axis-label:not([hidden])");
        var layeredLabelCount = await layeredLabels.CountAsync();
        layeredLabelCount.Should().BeGreaterThanOrEqualTo(5);
        layeredLabelCount.Should().BeLessThanOrEqualTo(10);
        ((await layeredLabels.First.InnerTextAsync()) ?? string.Empty).Trim().Should().NotBeEmpty();
        ((await layeredLabels.Last.InnerTextAsync()) ?? string.Empty).Trim().Should().NotBeEmpty();
        await AssertAxisEdgeLabelsAlignedWithinBoundsAsync(firstCard.Locator(".schedule-layered-axis").First);
        var layeredTodayColor = await firstCard.Locator(".schedule-layered-marker.today").First.EvaluateAsync<string>(
            "node => getComputedStyle(node).backgroundColor");
        layeredTodayColor.Should().Be("rgb(17, 17, 17)");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task HarmonogramAxes_ShouldKeepDateLabelsVisible_AfterRepeatedTabSwitches()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?asUser={_fixture.AdminOsobaId}");

        for (var attempt = 0; attempt < 3; attempt += 1)
        {
            await page.Locator("[data-tab='harmonogram']").ClickAsync();
            if (await page.Locator(".schedule-card").CountAsync() > 0)
            {
                var firstCard = page.Locator(".schedule-card").First;
                var labels = firstCard.Locator(".schedule-overview-axis .timeline-axis-label:not([hidden])");
                await Expect(labels.First).ToBeVisibleAsync();
                (await labels.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
            }

            await page.Locator("[data-tab='zaznamy']").ClickAsync();
        }

        await page.Locator("[data-tab='harmonogram']").ClickAsync();
        if (await page.Locator(".schedule-card").CountAsync() == 0)
        {
            await Expect(page.Locator("[data-project-schedule-list]")).ToHaveCountAsync(1);
            await page.Context.CloseAsync();
            return;
        }

        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();
        await page.Locator("[data-tab='harmonogram']").ClickAsync();

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
        }

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task RecordCreateSchedule_ShouldAllowDurationControls_AndReturnJsonOnSave()
    {
        var page = await _fixture.NewPageAsync();
        const string createdRecordName = "E2E create schedule edit";

        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
            await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
            await page.ReloadAsync();

            await page.GetByRole(AriaRole.Button, new() { Name = "Nový záznam" }).ClickAsync();

            var modal = page.Locator(".modal-overlay");
            var form = modal.Locator("form[data-record-editor-form='true']");
            await Expect(form).ToBeVisibleAsync();

            await form.Locator("input[name='Nazev']").FillAsync(createdRecordName);
            var subsystemSelect = form.Locator("select[name='Subsystem']");
            var subsystemOptions = await subsystemSelect.Locator("option").CountAsync();
            subsystemOptions.Should().BeGreaterThan(0, "nový záznam musí mít minimálně jeden aktivní subsystém");
            var subsystemValue = await subsystemSelect.InputValueAsync();
            subsystemValue.Should().NotBeNullOrWhiteSpace("subsystém je povinný pro uložení záznamu");

            var ownerCandidates = await form.Locator("[data-record-owner-picker] [data-person-picker-source] [data-id]").CountAsync();
            ownerCandidates.Should().BeGreaterThan(0, "vlastník musí být vybratelný ze seznamu osob projektu");
            await form.EvaluateAsync(
                @"formElement => {
                    const firstCandidate = formElement.querySelector('[data-record-owner-picker] [data-person-picker-source] [data-id]');
                    const hiddenInput = formElement.querySelector('[data-record-owner-picker] [data-person-picker-hidden]');
                    const queryInput = formElement.querySelector('[data-record-owner-picker] [data-person-picker-input]');
                    if (!(firstCandidate instanceof HTMLElement)
                        || !(hiddenInput instanceof HTMLInputElement)
                        || !(queryInput instanceof HTMLInputElement)) {
                        return;
                    }

                    const id = (firstCandidate.dataset.id || '').trim();
                    const label = (firstCandidate.dataset.label || '').trim();
                    const email = (firstCandidate.dataset.email || '').trim();
                    hiddenInput.value = id;
                    queryInput.value = email ? `${label} <${email}>` : label;
                    queryInput.setCustomValidity('');
                }");

            var categorySelect = form.Locator("select[name='Kategorie']");
            var taskCategoryValue = await categorySelect.EvaluateAsync<string>(
                @"select => {
                    const option = Array.from(select.options).find(item => /ukol|úkol/i.test((item.textContent || '').trim()));
                    return option ? option.value : '';
                }");
            taskCategoryValue.Should().NotBeNullOrWhiteSpace();
            await categorySelect.SelectOptionAsync(new SelectOptionValue { Value = taskCategoryValue });

            await modal.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();

            var firstDurationInput = form.Locator("[data-schedule-duration]").First;
            var firstDurationInc = form.Locator("[data-schedule-duration-inc]").First;

            await Expect(firstDurationInput).ToBeEnabledAsync();
            await Expect(firstDurationInc).ToBeEnabledAsync();

            var initialDurationValue = await firstDurationInput.InputValueAsync();
            var initialDuration = int.TryParse(initialDurationValue, out var parsedDuration) ? parsedDuration : 0;

            await firstDurationInc.ClickAsync();
            await Expect(firstDurationInput).ToHaveValueAsync((initialDuration + 1).ToString());

            var durationAfterStepper = await firstDurationInput.InputValueAsync();
            var firstDateInput = form.Locator("[data-schedule-date]").First;
            await firstDateInput.EvaluateAsync(
                @"input => {
                    const parseIso = (value) => {
                        const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec((value || '').trim());
                        if (!match) {
                            return null;
                        }

                        return new Date(Number.parseInt(match[1], 10), Number.parseInt(match[2], 10) - 1, Number.parseInt(match[3], 10));
                    };
                    const toIso = (value) => {
                        const year = value.getFullYear();
                        const month = String(value.getMonth() + 1).padStart(2, '0');
                        const day = String(value.getDate()).padStart(2, '0');
                        return `${year}-${month}-${day}`;
                    };

                    const selected = parseIso(input.value) || new Date();
                    selected.setDate(selected.getDate() + 3);
                    input.value = toIso(selected);
                    input.dispatchEvent(new Event('change', { bubbles: true }));
                }");

            (await firstDurationInput.InputValueAsync()).Should().NotBe(durationAfterStepper);

            var saveResponseTask = page.WaitForResponseAsync(response =>
                response.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && response.Url.Contains("/Zaznamy/Save", StringComparison.OrdinalIgnoreCase));

            await form.Locator("button[type='submit']").ClickAsync();

            var saveResponse = await saveResponseTask;
            var saveStatusCode = saveResponse.Status;
            var savePayload = await saveResponse.TextAsync();
            saveResponse.Ok.Should().BeTrue($"status: {saveStatusCode}, payload: {savePayload}");

            var headers = await saveResponse.AllHeadersAsync();
            headers.TryGetValue("content-type", out var contentType);
            contentType.Should().NotBeNullOrWhiteSpace();
            contentType!.Should().Contain("application/json");

            await Expect(page.Locator(".modal-overlay")).ToHaveCountAsync(0);
        }
        finally
        {
            await DeleteRecordByNameAsync(createdRecordName);
            await page.Context.CloseAsync();
        }
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }

    private static async Task AssertAxisEdgeLabelsAlignedWithinBoundsAsync(ILocator axis)
    {
        var result = await axis.EvaluateAsync<AxisEdgeAlignmentResult>(
            """
            axisNode => {
                if (!(axisNode instanceof HTMLElement)) {
                    return { hasEnoughLabels: false, firstInside: false, lastInside: false, firstAligned: false, lastAligned: false, firstFullyVisible: false, lastFullyVisible: false };
                }

                const labels = Array.from(axisNode.querySelectorAll('.timeline-axis-label'))
                    .filter(label => label instanceof HTMLElement && !label.hidden && (label.textContent || '').trim().length > 0);
                if (labels.length < 2) {
                    return { hasEnoughLabels: false, firstInside: false, lastInside: false, firstAligned: false, lastAligned: false, firstFullyVisible: false, lastFullyVisible: false };
                }

                const axisRect = axisNode.getBoundingClientRect();
                const firstLabel = labels[0];
                const lastLabel = labels[labels.length - 1];
                const firstTick = firstLabel.closest('.timeline-axis-tick');
                const lastTick = lastLabel.closest('.timeline-axis-tick');
                if (!(firstTick instanceof HTMLElement) || !(lastTick instanceof HTMLElement)) {
                    return { hasEnoughLabels: false, firstInside: false, lastInside: false, firstAligned: false, lastAligned: false, firstFullyVisible: false, lastFullyVisible: false };
                }

                const tolerance = 2.5;
                const firstRect = firstLabel.getBoundingClientRect();
                const lastRect = lastLabel.getBoundingClientRect();
                const firstTickRect = firstTick.getBoundingClientRect();
                const lastTickRect = lastTick.getBoundingClientRect();
                const widthTolerance = 1;

                return {
                    hasEnoughLabels: true,
                    firstInside: firstRect.left >= axisRect.left - tolerance && firstRect.right <= axisRect.right + tolerance,
                    lastInside: lastRect.left >= axisRect.left - tolerance && lastRect.right <= axisRect.right + tolerance,
                    firstAligned: Math.abs(firstRect.left - firstTickRect.left) <= tolerance,
                    lastAligned: Math.abs(lastRect.right - lastTickRect.left) <= tolerance,
                    firstFullyVisible: firstLabel.scrollWidth <= firstLabel.clientWidth + widthTolerance,
                    lastFullyVisible: lastLabel.scrollWidth <= lastLabel.clientWidth + widthTolerance
                };
            }
            """);

        result.HasEnoughLabels.Should().BeTrue();
        result.FirstInside.Should().BeTrue();
        result.LastInside.Should().BeTrue();
        result.FirstAligned.Should().BeTrue();
        result.LastAligned.Should().BeTrue();
        result.FirstFullyVisible.Should().BeTrue();
        result.LastFullyVisible.Should().BeTrue();
    }

    private sealed class AxisEdgeAlignmentResult
    {
        public bool HasEnoughLabels { get; set; }
        public bool FirstInside { get; set; }
        public bool LastInside { get; set; }
        public bool FirstAligned { get; set; }
        public bool LastAligned { get; set; }
        public bool FirstFullyVisible { get; set; }
        public bool LastFullyVisible { get; set; }
    }

    private async Task DeleteRecordByNameAsync(string recordName)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var findCommand = connection.CreateCommand();
        findCommand.CommandText = """
            SELECT TOP (1) id
            FROM dbo.projektove_zaznamy
            WHERE projekt_id = @projectId
              AND nazev = @recordName
            ORDER BY id DESC;
            """;
        findCommand.Parameters.AddWithValue("@projectId", _fixture.ProjectId);
        findCommand.Parameters.AddWithValue("@recordName", recordName);
        var result = await findCommand.ExecuteScalarAsync();
        if (result is null || result == DBNull.Value)
        {
            return;
        }

        var recordId = Convert.ToInt32(result);
        var dependentTables = new[]
        {
            "zaznam_harmonogram_hodnoty",
            "zaznam_spoluprace",
            "zaznam_externi_odkazy",
            "vyjadreni",
            "zaznam_historie_zmen_typu",
            "zaznam_historie_terminu",
            "zaznam_historie_vlastnik",
            "zaznam_historie_subsystem",
            "zaznam_historie_stavu_zaznamu",
            "zaznam_historie_stavu_projektu"
        };

        foreach (var table in dependentTables)
        {
            await using var deleteDependencyCommand = connection.CreateCommand();
            deleteDependencyCommand.CommandText = $"DELETE FROM dbo.{table} WHERE zaznam_id = @recordId;";
            deleteDependencyCommand.Parameters.AddWithValue("@recordId", recordId);
            await deleteDependencyCommand.ExecuteNonQueryAsync();
        }

        await using var deleteRecordCommand = connection.CreateCommand();
        deleteRecordCommand.CommandText = "DELETE FROM dbo.projektove_zaznamy WHERE id = @recordId;";
        deleteRecordCommand.Parameters.AddWithValue("@recordId", recordId);
        await deleteRecordCommand.ExecuteNonQueryAsync();
    }
}
