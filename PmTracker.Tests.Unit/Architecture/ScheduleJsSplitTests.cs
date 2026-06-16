using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3B Task 2: schedule.js (1697 LOC, 24+ exports) rozdělen
/// do 4 feature modulů + orchestrator + backward-compat barrel re-export.
/// </summary>
public sealed class ScheduleJsSplitTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        return directory.FullName;
    }

    private static string ResolvePath(string relative) =>
        Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    [Theory]
    [InlineData("PmTracker.Web/wwwroot/js/modules/schedule/filters.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/schedule/gantt.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/schedule/timeline.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/schedule/block.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/schedule/index.js")]
    public void Submodule_ShouldExistAndNotBeEmpty(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3B Task 2");
        File.ReadAllText(full).Length.Should().BeGreaterThan(500);
    }

    [Fact]
    public void BarrelModule_ShouldReExport()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/schedule.js"));
        content.Should().Contain("export * from \"./schedule/index.js\"",
            "schedule.js je backward-compat barrel");
        File.ReadAllLines(ResolvePath("PmTracker.Web/wwwroot/js/modules/schedule.js")).Length
            .Should().BeLessThan(50, "barrel je jen re-export");
    }

    [Fact]
    public void GanttModule_ShouldContainProjectGanttBoardClass()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/schedule/gantt.js"));
        content.Should().Contain("class ProjectGanttBoard");
    }

    [Fact]
    public void BlockModule_ShouldContainScheduleBlockRendererClass()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/schedule/block.js"));
        content.Should().Contain("class ScheduleBlockRenderer");
        content.Should().NotContain("class ProjectGanttBoard", "gantt patří do gantt.js");
    }

    [Fact]
    public void TimelineModule_ShouldContainAxisFunctions()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/schedule/timeline.js"));
        content.Should().Contain("renderTimelineAxis");
        content.Should().Contain("buildTimelineAxisTicks");
    }

    [Fact]
    public void FiltersModule_ShouldContainFilterApplication()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/schedule/filters.js"));
        content.Should().Contain("applyProjectScheduleFilters");
    }

    [Fact]
    public void IndexModule_ShouldExportPlannerInit()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/schedule/index.js"));
        content.Should().Contain("initRecordSchedulePlanner");
        content.Should().Contain("initProjectScheduleUi");
    }

    [Fact]
    public void Bundle_ShouldContainKeyExports()
    {
        var bundle = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/site.bundle.js"));
        bundle.Should().Contain("class ProjectGanttBoard");
        bundle.Should().Contain("class ScheduleBlockRenderer");
        bundle.Should().Contain("renderTimelineAxis");
        bundle.Should().Contain("applyProjectScheduleFilters");
        bundle.Should().Contain("initRecordSchedulePlanner");
    }

    [Fact]
    public void BlockJs_RenderSummary_PouzivaAktualniKrok_NeStihameVsTermin()
    {
        // Datum-model: editor live-preview souhrn = sjednocený stav (aktuální krok + znaménkové
        // překročení), ne staré „Stíháme/Nestíháme vs deadline".
        var src = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/schedule/block.js"));
        src.Should().NotContain("Nestíháme", "souhrn už nepoužívá Stíháme/Nestíháme");
        src.Should().Contain("aktualniIndex", "computeDateModel počítá aktuální krok");
        src.Should().Contain("Aktuální krok:", "renderSummary sestavuje sjednocený stav");
    }

    [Fact]
    public void Chronologie_PmDateFieldPodporujeMinMax_PickerDisablujeMimoRozsah_BlockJsNastavujeBounds()
    {
        var pmDateField = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/components/pm-date-field.js"));
        pmDateField.Should().Contain("ATTR_MIN").And.Contain("ATTR_MAX", "pm-date-field zná min/max atributy");

        var picker = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/pickers/date.js"));
        picker.Should().Contain("getAttribute(\"min\")").And.Contain("getAttribute(\"max\")",
            "kalendář čte min/max z host elementu");
        picker.Should().Contain("out-of-range", "mimo-rozsah dny se disablují");

        var block = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/schedule/block.js"));
        block.Should().Contain("applyPlanChronologyBounds", "block.js nastavuje min/max z sousedních kroků");
    }
}
