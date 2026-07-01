using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// 2026-06-30: renderer přešel z ručního SVG na Apache ECharts. <c>_Chart.cshtml</c> už
/// nevolá <c>_ChartBar.cshtml</c> (mrtvý), ale pro grafové druhy emituje kontejner
/// <c>[data-echart]</c> s <see cref="PmTracker.Web.Services.ProjectDashboard.Zakladni.ChartData"/>
/// serializovaným do <c>data-chart-json</c>. StatCards dál jdou přes HTML partial.
/// </summary>
public sealed class ChartRendererTests
{
    private static string Read(string rel)
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, rel.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void Chart_dispatch_renders_echart_container_for_graphs_and_statcards_partial()
    {
        var v = Read("PmTracker.Web/Views/ProjectDashboard/Zakladni/_Chart.cshtml");
        v.Should().Contain("data-echart");
        v.Should().Contain("data-chart-json");
        v.Should().Contain("ChartJson.Serialize");
        v.Should().Contain("_ChartStatCards.cshtml");
        v.Should().NotContain("_ChartBar.cshtml");
    }
}
