using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// 2026-06-30: po přechodu na ECharts je ručně psaný SVG bar mrtvý kód. Tento test hlídá,
/// že partial i jeho CSS pravidla zmizely (žádný zombie renderer).
/// </summary>
public sealed class DeadSvgRemovedTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PmTracker.sln"))) d = d.Parent;
        return d!.FullName;
    }

    [Fact]
    public void Svg_bar_partial_is_gone()
        => File.Exists(Path.Combine(Root(), "PmTracker.Web/Views/ProjectDashboard/Zakladni/_ChartBar.cshtml".Replace('/', Path.DirectorySeparatorChar)))
            .Should().BeFalse("ruční SVG bar nahradil ECharts");

    [Fact]
    public void Dead_svg_bar_css_is_gone()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "PmTracker.Web/wwwroot/css/components/zakladni-report.css".Replace('/', Path.DirectorySeparatorChar)));
        css.Should().NotContain(".zr-bar-rect");
        css.Should().NotContain(".zr-bar-value");
        css.Should().NotContain(".zr-bar-label");
    }
}
