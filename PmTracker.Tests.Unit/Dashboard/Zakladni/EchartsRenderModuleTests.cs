using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// 2026-06-30: klientský renderer základního reportu přes Apache ECharts. File-text kontrakt:
/// importuje vendrovaný offline ESM build, vykresluje SVG, větví podle <c>kind</c>, a je
/// zadrátovaný v bootstrapu (init + resize) i v reload cestě reportu (re-init po výměně HTML).
/// </summary>
public sealed class EchartsRenderModuleTests
{
    private static string Read(string rel)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PmTracker.sln"))) d = d.Parent;
        return File.ReadAllText(Path.Combine(d!.FullName, rel.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void Module_imports_vendored_echarts_and_renders_svg_by_kind()
    {
        var js = Read("PmTracker.Web/wwwroot/js/modules/dashboard/echartsRender.js");
        js.Should().Contain("../../../lib/echarts/echarts.esm.min.js");
        js.Should().Contain("renderer: \"svg\"");
        js.Should().Contain("data-echart");
        foreach (var k in new[] { "Bar", "StackedBar", "Pie", "Line" })
        {
            js.Should().Contain(k);
        }
    }

    [Fact]
    public void Bootstrap_wires_echarts_report()
        => Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js").Should().Contain("initEchartsReport");

    [Fact]
    public void ReportReload_reinitializes_echarts()
        => Read("PmTracker.Web/wwwroot/js/modules/dashboard/zakladniReport.js").Should().Contain("initEchartsReport");
}
