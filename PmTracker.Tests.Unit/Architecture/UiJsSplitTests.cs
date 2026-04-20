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

    [Fact]
    public void Bundle_ShouldContainKeyExports()
    {
        var bundle = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/site.bundle.js"));
        bundle.Should().Contain("closeAllFloatingPanels");
        bundle.Should().Contain("handlePrintTriggerClick");
        bundle.Should().Contain("renderAllRainbowSegmentLabels");
        bundle.Should().Contain("getGlobalFloatingLayerRoot");
    }
}
