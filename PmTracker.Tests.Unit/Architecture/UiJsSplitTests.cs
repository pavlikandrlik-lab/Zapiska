using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class UiJsSplitTests
{
    [Theory]
    [InlineData("PmTracker.Web/wwwroot/js/modules/ui/print.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/ui/floating.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/ui/index.js")]
    public void Submodule_ShouldExistAndNotBeEmpty(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3B Task 5");
        File.ReadAllText(full).Length.Should().BeGreaterThan(300);
    }

    [Fact]
    public void BarrelModule_ShouldReExport()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/ui.js"));
        content.Should().Contain("export * from \"./ui/index.js\"");
        File.ReadAllLines(ResolvePath("PmTracker.Web/wwwroot/js/modules/ui.js")).Length
            .Should().BeLessThan(50, "barrel je jen re-export");
    }

    [Fact]
    public void PrintModule_ShouldContainPrintChooser()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/ui/print.js"));
        content.Should().Contain("handlePrintTriggerClick");
        content.Should().Contain("initPrintFormatChooser");
        content.Should().NotContain("mountFloatingPanel", "floating patří do floating.js");
    }

    [Fact]
    public void FloatingModule_ShouldContainPanelSystem()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/ui/floating.js"));
        content.Should().Contain("closeAllFloatingPanels");
        content.Should().Contain("queueFloatingPanelReposition");
        content.Should().NotContain("initPrintFormatChooser", "print patří do print.js");
    }

    [Fact]
    public void IndexModule_ShouldContainRainbowRender()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/ui/index.js"));
        content.Should().Contain("renderAllRainbowSegmentLabels");
    }

    /// <summary>
    /// 2026-06-29: rozpad (breakdown) má kroky pojmenované ve vlastní koloně (.gantt-step-name),
    /// takže popisek NA segmentu je redundantní — a skutečnostní čára kreslená přes plánovou ho
    /// překryla, takže zůstávaly zbytky textu. Rainbow popisky tedy patří jen na overview rainbow
    /// strip (a editor mini-gantt), NE na rozpadové .schedule-layered-segment.
    /// </summary>
    [Fact]
    public void RainbowLabels_ShouldTargetOverviewNotBreakdown()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/ui/index.js"));
        content.Should().Contain(".schedule-overview-segment[data-rainbow-segment-label-short]",
            "overview rainbow strip popisky kroků zůstávají");
        content.Should().NotContain(".schedule-layered-segment[data-rainbow-segment-label-short]",
            "rozpadové segmenty nesmí dostávat rainbow popisky (skutečnostní čára je překryje)");
    }

}
