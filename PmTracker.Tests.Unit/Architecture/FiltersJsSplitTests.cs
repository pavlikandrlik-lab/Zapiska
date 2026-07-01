using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3B Task 4: filters.js (1105 LOC, 18+ exportů) rozdělen
/// do 3 feature modulů + orchestrator + barrel re-export.
/// </summary>
public sealed class FiltersJsSplitTests
{
    [Theory]
    [InlineData("PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/filters/recordDisplay.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/filters/printFilter.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/filters/index.js")]
    public void Submodule_ShouldExistAndNotBeEmpty(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3B Task 4");
        File.ReadAllText(full).Length.Should().BeGreaterThan(300);
    }

    [Fact]
    public void BarrelModule_ShouldReExport()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/filters.js"));
        content.Should().Contain("export * from \"./filters/index.js\"");
        File.ReadAllLines(ResolvePath("PmTracker.Web/wwwroot/js/modules/filters.js")).Length
            .Should().BeLessThan(50, "barrel je jen re-export");
    }

    [Fact]
    public void ProjectFilterModule_ShouldContainConfigAndChips()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js"));
        content.Should().Contain("getProjectFilterConfig");
        content.Should().Contain("normalizeProjectFilterState");
        content.Should().Contain("renderProjectFilterChips");
        content.Should().NotContain("buildProjectPrintFilterSnapshot", "print patří do printFilter.js");
    }

    [Fact]
    public void RecordDisplayModule_ShouldContainVisibilityFunctions()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/filters/recordDisplay.js"));
        content.Should().Contain("applyProjectRecordFilters");
        content.Should().Contain("applyRecordsView");
        content.Should().Contain("initSubsystemScrollIndicator");
    }

    [Fact]
    public void PrintFilterModule_ShouldContainSnapshotHelpers()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/filters/printFilter.js"));
        content.Should().Contain("buildProjectPrintFilterSnapshot");
        content.Should().Contain("buildProjectPrintFilterQueryParams");
    }

    [Fact]
    public void IndexModule_ShouldExportInitProjectRecordsUi()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/filters/index.js"));
        content.Should().Contain("initProjectRecordsUi");
        content.Should().Contain("setFilterPanelOpen");
    }

}
