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

    // Úklid 2026-11-11: testy níže dřív otevíraly editor v modálu („Upravit“ na kartě harmonogramu,
    // „Nový záznam“ jako tlačítko) — ten od 2026-05-01 neexistuje, editor je stránka /Zaznamy/Edit
    // a /Zaznamy/Create. Běží na vlastním projektu s úkolem s plánem 10 kroků (E2EOwnProject), takže
    // kartu harmonogramu mají vždy — dřív při prázdném projektu mlčky prošly. Značky „dnes“ a
    // „termín“ jsou od přestavby osy události osy (.timeline-axis-event), ne značky na pruhu.

    [Fact]
    public async Task Harmonogram_ShouldRenderOverviewAndLayeredBreakdown_WithTodayAndDeadlineOnAxes()
    {
        var (project, _) = await CreateProjectWithScheduledTaskAsync();
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{project.ProjectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");

            var card = page.Locator(".schedule-card");
            await Expect(card).ToHaveCountAsync(1);
            var overviewAxis = card.Locator(".schedule-overview-axis");
            await Expect(card.Locator(".schedule-overview-row")).ToHaveCountAsync(2);
            await Expect(overviewAxis).ToHaveCountAsync(1);
            await Expect(overviewAxis.Locator(".timeline-axis-event.today")).ToHaveCountAsync(1);
            await Expect(overviewAxis.Locator(".timeline-axis-event.deadline")).ToHaveCountAsync(1);
            await Expect(page.Locator(".tab-panel[data-tab-panel='harmonogram'] .schedule-mini-legend")).ToHaveCountAsync(0);
            await Expect(card.Locator(".schedule-layered-legend--steps")).ToHaveCountAsync(1);
            await Expect(card.Locator(".schedule-layered-axis")).ToHaveCountAsync(1);
            await Expect(card.Locator(".schedule-layered-track--step")).ToHaveCountAsync(10);
            await AssertAxisEdgeLabelsAlignedWithinBoundsAsync(overviewAxis);

            // Rozpad: skrytý → otevřít → zavřít → otevřít (pm-button host je pro Playwright „not visible“).
            var toggle = card.Locator("[data-schedule-expand-toggle]");
            var breakdown = card.Locator("[data-schedule-steps]");
            await Expect(breakdown).ToBeHiddenAsync();
            await toggle.DispatchEventAsync("click");
            await Expect(breakdown).ToBeVisibleAsync();
            await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "true");
            await toggle.DispatchEventAsync("click");
            await Expect(breakdown).ToBeHiddenAsync();
            await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");
            await toggle.DispatchEventAsync("click");
            await Expect(breakdown).ToBeVisibleAsync();

            var layeredAxis = card.Locator(".schedule-layered-axis");
            await Expect(layeredAxis.Locator(".timeline-axis-event.today")).ToHaveCountAsync(1);
            await Expect(layeredAxis.Locator(".timeline-axis-label:not([hidden])").First).ToBeVisibleAsync();
            await AssertAxisEdgeLabelsAlignedWithinBoundsAsync(layeredAxis);
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }

    [Fact]
    public async Task RecordEditorScheduleTab_ShouldRenderSingleMiniGanttAxis_WithVisibleDateLabels()
    {
        var (_, recordId) = await CreateProjectWithScheduledTaskAsync();
        var page = await OpenEditorScheduleTabAsync(recordId);
        try
        {
            var form = page.Locator("form[data-record-editor-form='true']");
            var axes = form.Locator("[data-schedule-axis]");
            await Expect(axes).ToHaveCountAsync(1);

            var labels = axes.First.Locator(".timeline-axis-label:not([hidden])");
            await Expect(labels.First).ToBeVisibleAsync();
            (await labels.CountAsync()).Should().BeGreaterThanOrEqualTo(2);
            ((await labels.First.InnerTextAsync()) ?? string.Empty).Trim().Should().NotBeEmpty();
            await Expect(axes.First.Locator(".timeline-axis-event.today")).ToHaveCountAsync(1);
            await Expect(axes.First.Locator(".timeline-axis-event.deadline")).ToHaveCountAsync(1);
            await AssertAxisEdgeLabelsAlignedWithinBoundsAsync(axes.First);
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }

    [Fact]
    public async Task RecordEditorScheduleTab_ShouldRenderPlanAndActualMiniGanttAsTwoSeparateRows()
    {
        var (_, recordId) = await CreateProjectWithScheduledTaskAsync();
        var page = await OpenEditorScheduleTabAsync(recordId);
        try
        {
            var rows = page.Locator("form[data-record-editor-form='true'] [data-record-mini-gantt] .schedule-mini-gantt-row");
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
            firstTrackBox!.Width.Should().BeGreaterThan(40);
            secondTrackBox!.Width.Should().BeGreaterThan(40);
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }

    [Fact]
    public async Task HarmonogramAxes_ShouldKeepDateLabelsVisible_AfterRepeatedTabSwitches()
    {
        var (project, recordId) = await CreateProjectWithScheduledTaskAsync();
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{project.ProjectId}?asUser={_fixture.AdminOsobaId}");
            for (var attempt = 0; attempt < 3; attempt += 1)
            {
                await page.Locator("[data-tab='harmonogram']").ClickAsync();
                var labels = page.Locator(".schedule-card").First.Locator(".schedule-overview-axis .timeline-axis-label:not([hidden])");
                await Expect(labels.First).ToBeVisibleAsync();
                (await labels.CountAsync()).Should().BeGreaterThanOrEqualTo(2, $"přepnutí č. {attempt + 1}");
                await page.Locator("[data-tab='zaznamy']").ClickAsync();
            }

            await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
            var axis = page.Locator("form[data-record-editor-form='true'] [data-schedule-axis]").First;
            for (var attempt = 0; attempt < 3; attempt += 1)
            {
                await page.Locator("pm-tabs [key='basic']").First.ClickAsync();
                await page.Locator("pm-tabs [key='schedule']").First.ClickAsync();
                var labels = axis.Locator(".timeline-axis-label:not([hidden])");
                await Expect(labels.First).ToBeVisibleAsync();
                (await labels.CountAsync()).Should().BeGreaterThanOrEqualTo(2, $"přepnutí v editoru č. {attempt + 1}");
            }
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }

    [Fact]
    public async Task RecordCreate_TaskWithPlanDate_ShowsDaysAndSavesSchedule()
    {
        const string createdRecordName = "E2E novy ukol s planem";
        var termin = DateTime.Today.AddDays(30);
        var plan1 = DateTime.Today.AddDays(7);
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Create?projektId={_fixture.ProjectId}&asUser={_fixture.AdminOsobaId}");
            var form = page.Locator("form[data-record-editor-form='true']");
            await Expect(form).ToHaveCountAsync(1);

            await form.Locator("input[name='Nazev']").FillAsync(createdRecordName);
            await form.Locator("select[name='Kategorie']").SelectOptionAsync(new SelectOptionValue { Label = "Úkol" });
            // Projekt fixture nemá výchozího vlastníka (VlastnikId = 0) — vybrat jako uživatel.
            await form.Locator("[data-record-owner-picker] [data-person-picker-input]").FillAsync("Pavel");
            await page.Locator("[data-person-picker-panel] button.office-search-item[role='option']").First.ClickAsync();
            await Expect(form.Locator("input[name='VlastnikId']")).ToHaveValueAsync(_fixture.AdminOsobaId.ToString());

            var terminDay = await OpenCalendarAtAsync(page, form.Locator("pm-date-field:has(input[name='TerminUkonceni'])"), termin);
            await terminDay.DispatchEventAsync("click");

            await page.Locator("pm-tabs [key='schedule']").First.ClickAsync();
            var row1 = page.Locator("tr[data-schedule-step-row][data-step-index='1']");
            var planDay = await OpenCalendarAtAsync(page, row1.Locator("pm-date-field[data-schedule-plan-step]"), plan1);
            await Expect(planDay).ToBeEnabledAsync();
            await planDay.DispatchEventAsync("click");
            await Expect(row1.Locator("[data-schedule-duration-readonly]")).ToHaveTextAsync("7 dnů");

            var saveTask = page.WaitForResponseAsync(response =>
                response.Request.Method == "POST"
                && response.Url.Contains("/Zaznamy/Save", StringComparison.OrdinalIgnoreCase));
            await form.EvaluateAsync("form => form.requestSubmit()");
            var save = await saveTask;
            // Tělo odpovědi číst nejde — editor po uložení naviguje a Chromium ho zahodí.
            save.Ok.Should().BeTrue($"uložení vrátilo HTTP {save.Status}");

            var saved = await LoadStepPlanAsync(createdRecordName, poradi: 1);
            saved.Should().Be(plan1, "plán kroku 1 z editoru se uložil k novému úkolu");
        }
        finally
        {
            await page.Context.CloseAsync();
            await DeleteRecordByNameAsync(createdRecordName);
        }
    }

    [Fact]
    public async Task Design9F_DurationCell_ShouldShowReadonlyDaysToTheRightOfCalendar()
    {
        // DESIGN-9-F (2026-05-02): počet dnů je vpravo vedle kalendáře plánu, ve stejné buňce.
        var (_, recordId) = await CreateProjectWithScheduledTaskAsync();
        var page = await OpenEditorScheduleTabAsync(recordId);
        try
        {
            var firstRow = page.Locator("form[data-record-editor-form='true'] [data-schedule-step-row]").First;
            await Expect(firstRow).ToBeVisibleAsync();

            var layout = await firstRow.EvaluateAsync<DurationLayoutResult>(
                @"row => {
                    const cell = row.querySelector('td:nth-child(2)'); // sloupec plánu
                    const cal = cell?.querySelector('[data-schedule-duration-calendar]');
                    const readonlyEl = cell?.querySelector('[data-schedule-duration-readonly]');
                    if (!cal || !readonlyEl) {
                        return { hasCalendar: !!cal, hasReadonly: !!readonlyEl, calRight: 0, readonlyLeft: 0, sameRow: false, readonlyText: '' };
                    }
                    const cr = cal.getBoundingClientRect();
                    const rr = readonlyEl.getBoundingClientRect();
                    return {
                        hasCalendar: true,
                        hasReadonly: true,
                        calRight: cr.right,
                        readonlyLeft: rr.left,
                        sameRow: Math.abs(cr.top - rr.top) < 30,
                        readonlyText: (readonlyEl.textContent || '').trim()
                    };
                }");

            layout.HasCalendar.Should().BeTrue("kalendář musí být ve sloupci plánu");
            layout.HasReadonly.Should().BeTrue("počet dnů musí být ve sloupci plánu");
            layout.SameRow.Should().BeTrue("kalendář a počet dnů mají být na stejném řádku (rozdíl top < 30 px)");
            layout.ReadonlyLeft.Should().BeGreaterThan(layout.CalRight - 5,
                $"počet dnů musí být VPRAVO od kalendáře. calRight={layout.CalRight} readonlyLeft={layout.ReadonlyLeft}");
            layout.ReadonlyText.Should().MatchRegex(@"\d+\s+(den|dny|dnů)",
                $"text musí být '{{N}} den/dny/dnů'. Actual: '{layout.ReadonlyText}'");
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }

    /// <summary>Vlastní projekt s jedním aktivním úkolem: založen před 30 dny, termín za 45 dní, plán 10 kroků po týdnu.</summary>
    private async Task<(E2EOwnProject Project, int RecordId)> CreateProjectWithScheduledTaskAsync()
    {
        var project = await E2EOwnProject.CreateAsync(_fixture.Database.ConnectionString, _fixture.AdminOsobaId);
        var recordId = await project.AddTaskAsync(1, project.FirstSubsystemId, withSchedule: true);
        return (project, recordId);
    }

    private async Task<IPage> OpenEditorScheduleTabAsync(int recordId)
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        await page.Locator("pm-tabs [key='schedule']").First.ClickAsync();
        return page;
    }

    /// <summary>Otevře kalendář pole a šipkami ‹ › přejde na měsíc daného dne; vrátí tlačítko dne (vzor ScheduleLiveRecalcScenariosTests).</summary>
    private static async Task<ILocator> OpenCalendarAtAsync(IPage page, ILocator dateField, DateTime day)
    {
        await dateField.Locator("[data-app-date-open]").DispatchEventAsync("click");
        var panel = page.Locator("[data-app-date-panel]:not([hidden])");
        await Expect(panel).ToHaveCountAsync(1);
        var shownMonth = int.Parse(await panel.Locator("[data-app-date-month]").InputValueAsync());
        var shownYear = int.Parse(await panel.Locator("[data-app-date-year]").InputValueAsync());
        var diff = (day.Year * 12 + day.Month - 1) - (shownYear * 12 + shownMonth);
        for (var i = 0; i < Math.Abs(diff); i++)
        {
            await panel.Locator(diff > 0 ? "[data-app-date-next]" : "[data-app-date-prev]").DispatchEventAsync("click");
        }
        return panel.Locator($"button.app-date-day[data-iso='{day:yyyy-MM-dd}']");
    }

    private async Task<DateTime?> LoadStepPlanAsync(string recordName, int poradi)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1) k.plan_datum
            FROM dbo.zaznam_harmonogram_krok k
            JOIN dbo.projektove_zaznamy z ON z.id = k.zaznam_id
            WHERE z.projekt_id = @projectId AND z.nazev = @recordName AND k.poradi = @poradi
            ORDER BY z.id DESC;
            """;
        command.Parameters.AddWithValue("@projectId", _fixture.ProjectId);
        command.Parameters.AddWithValue("@recordName", recordName);
        command.Parameters.AddWithValue("@poradi", poradi);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToDateTime(value);
    }

    private sealed class DurationLayoutResult
    {
        public bool HasCalendar { get; set; }
        public bool HasReadonly { get; set; }
        public double CalRight { get; set; }
        public double ReadonlyLeft { get; set; }
        public bool SameRow { get; set; }
        public string ReadonlyText { get; set; } = string.Empty;
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
                // timeline.js layoutAxisLabels: popisek prvního ticku začíná na gridline, popisek
                // posledního ticku na ní končí, ostatní jsou vycentrované. Popisek, který by překryl
                // DNES / TERMÍN, se skryje (layoutAxisEventMarkers) — krajní viditelný popisek pak
                // nemusí patřit krajnímu ticku a zarovnání ke kraji se u něj neověřuje.
                const ticks = Array.from(axisNode.querySelectorAll('.timeline-axis-tick'));
                const firstIsEdgeTick = ticks.indexOf(firstTick) === 0;
                const lastIsEdgeTick = ticks.indexOf(lastTick) === ticks.length - 1;

                return {
                    hasEnoughLabels: true,
                    firstInside: firstRect.left >= axisRect.left - tolerance && firstRect.right <= axisRect.right + tolerance,
                    lastInside: lastRect.left >= axisRect.left - tolerance && lastRect.right <= axisRect.right + tolerance,
                    firstAligned: !firstIsEdgeTick || Math.abs(firstRect.left - firstTickRect.left) <= tolerance,
                    lastAligned: !lastIsEdgeTick || Math.abs(lastRect.right - lastTickRect.left) <= tolerance,
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
            // zaznam_harmonogram_hodnoty zrušila db_upgrade_1_4_0; vazby na externí odkazy (NO ACTION) před nimi.
            "zaznam_harmonogram_vyjadreni_vazba",
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
