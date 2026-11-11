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
    public void Module_imports_vendored_echarts_and_renders_svg()
    {
        var js = Read("PmTracker.Web/wwwroot/js/modules/dashboard/echartsRender.js");
        js.Should().Contain("../../../lib/echarts/echarts.esm.min.js");
        js.Should().Contain("renderer: \"svg\"");
        js.Should().Contain("data-echart");
    }

    /// <summary>
    /// Mapování ChartData → ECharts option je čistá funkce v echartsOption.js (chování testuje
    /// tests/js/dom/echartsOption.test.js). Tady jen pojistka, že každý grafový ChartKind má větev.
    /// </summary>
    [Fact]
    public void EchartsRender_mapuje_vsechny_kinds()
    {
        var js = Read("PmTracker.Web/wwwroot/js/modules/dashboard/echartsOption.js");
        var grafove = Enum.GetNames<PmTracker.Web.Services.ProjectDashboard.Zakladni.ChartKind>()
            .Where(k => k is not ("StatCards" or "Kpi")); // karty kreslí server, ne ECharts
        foreach (var k in grafove)
        {
            js.Should().Contain($"case \"{k}\":", $"ChartKind.{k} musí mít větev v optionFor");
        }
    }

    /// <summary>Přepnutí motivu grafy přebarví bez reloadu; lazy panel je po načtení inicializuje.</summary>
    [Fact]
    public void Render_reaguje_na_motiv_a_nacteni_panelu()
    {
        var js = Read("PmTracker.Web/wwwroot/js/modules/dashboard/echartsRender.js");
        js.Should().Contain("MutationObserver");
        js.Should().Contain("data-theme");
        js.Should().Contain("pm:panel-loaded");
        js.Should().Contain("./echartsTheme.js");
        js.Should().Contain("./echartsOption.js");
    }

    [Fact]
    public void Bootstrap_wires_echarts_report()
        => Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js").Should().Contain("initEchartsReport");

    [Fact]
    public void ReportReload_reinitializes_echarts()
        => Read("PmTracker.Web/wwwroot/js/modules/dashboard/zakladniReport.js").Should().Contain("initEchartsReport");
}
