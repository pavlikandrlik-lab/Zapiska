using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úprava #4 (2026-04-20): user dashboard full-width + non-scrollable.
/// Layout 2/3 Záznamy vlevo, 1/3 pravý sloupec split vertikálně
/// (Jednání nahoře, News dole). Responsive fallback &lt;1024px na stacked scrollable.
/// </summary>
public sealed class DashboardLayoutTests
{
    [Fact]
    public void DashboardIndex_ShouldUseFullWidthShell()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/Index.cshtml"));
        view.Should().Contain("dashboard-shell",
            "Dashboard view používá .dashboard-shell (full-viewport height) wrapper");
        view.Should().Contain("data-dashboard-shell",
            "data-dashboard-shell marker pro JS scope + architecture testing");
    }

    [Fact]
    public void DashboardCss_ShouldDefineFullHeightGrid()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.dashboard-shell\s*\{[\s\S]*?height\s*:\s*100vh",
            "shell má 100vh height (non-scrollable outer)");
        css.Should().MatchRegex(
            @"\.dashboard-shell\s*\{[\s\S]*?overflow\s*:\s*hidden",
            "shell overflow hidden (non-scrollable outer)");
        css.Should().MatchRegex(
            @"\.dashboard-body\s*\{[\s\S]*?grid-template-columns\s*:\s*2fr\s+1fr",
            "body grid 2/3 + 1/3");
    }

    [Fact]
    public void DashboardCss_ShouldPlacePanelsIn2By3Layout()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.dashboard-section--focus[\s\S]*?grid-column\s*:\s*1[\s\S]*?grid-row\s*:\s*1\s*/\s*3",
            "Focus panel span vlevo celá výška");
        css.Should().MatchRegex(
            @"\.dashboard-section--meetings[\s\S]*?grid-column\s*:\s*2[\s\S]*?grid-row\s*:\s*1",
            "Meetings vpravo nahoře");
        css.Should().MatchRegex(
            @"\.dashboard-section--news[\s\S]*?grid-column\s*:\s*2[\s\S]*?grid-row\s*:\s*2",
            "News vpravo dole");
    }

    [Fact]
    public void DashboardCss_ShouldProvideResponsiveFallback()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().Contain("@media (max-width: 1024px)",
            "responsive fallback stacking pod 1024px");
    }
}
