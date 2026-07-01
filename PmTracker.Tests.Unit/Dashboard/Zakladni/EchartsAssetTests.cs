using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// 2026-06-30: charting přešel z ručního SVG na Apache ECharts. Aplikace běží v prostředí
/// bez internetu → ECharts musí být self-hostovaný (vendrovaný) ve wwwroot/lib, žádné CDN.
/// </summary>
public sealed class EchartsAssetTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PmTracker.sln")))
        {
            d = d.Parent;
        }

        return d!.FullName;
    }

    [Fact]
    public void Echarts_esm_build_is_vendored_offline()
    {
        var path = Path.Combine(Root(), "PmTracker.Web", "wwwroot", "lib", "echarts", "echarts.esm.min.js");
        File.Exists(path).Should().BeTrue("ECharts musí být self-hostovaný (offline), žádné CDN");
        new FileInfo(path).Length.Should().BeGreaterThan(100_000, "vendrovaný bundle ECharts");
    }

    [Fact]
    public void Echarts_license_is_vendored()
    {
        var path = Path.Combine(Root(), "PmTracker.Web", "wwwroot", "lib", "echarts", "LICENSE");
        File.Exists(path).Should().BeTrue("vendrovaná knihovna musí nést svou Apache-2.0 licenci");
    }
}
