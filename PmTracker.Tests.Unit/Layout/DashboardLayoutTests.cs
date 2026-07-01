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
        // Výška „přesně jedna obrazovka" se od 2026-06-08 počítá na
        // .app-main--fluid:has(.dashboard-shell) = 100dvh − hlavička (dřív
        // .dashboard-shell{height:100vh;overflow:hidden}; shell je teď flex a stránka smí scrollovat).
        css.Should().MatchRegex(
            @"\.app-main--fluid:has\(\.dashboard-shell\)\s*\{[\s\S]*?height\s*:\s*calc\(\s*100dvh",
            "výška dashboardu = 100dvh − hlavička na .app-main--fluid:has(.dashboard-shell)");
        css.Should().MatchRegex(
            @"\.dashboard-body\s*\{[\s\S]*?grid-template-columns\s*:\s*2fr\s+1fr",
            "body grid 2/3 (focus) + 1/3 (rail)");
    }

    [Fact]
    public void DashboardCss_ShouldPlaceFocusLeftAndRailRight_WithDynamicHeightSharing()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        // Focus vlevo (grid-column 1); pravý sloupec = .dashboard-rail (grid-column 2) flex-column,
        // kde Jednání + Novinky dynamicky sdílí výšku přes :has (panel s obsahem roste, prázdný se smrští).
        css.Should().MatchRegex(
            @"\.dashboard-section--focus\s*\{[\s\S]*?grid-column\s*:\s*1",
            "Focus panel vlevo (grid-column 1)");
        css.Should().MatchRegex(
            @"\.dashboard-rail\s*\{[\s\S]*?grid-column\s*:\s*2",
            "pravý sloupec (rail) vpravo (grid-column 2)");
        css.Should().MatchRegex(
            @"\.dashboard-rail\s*>\s*\.dashboard-panel:has\([\s\S]*?flex\s*:\s*1\s+1\s+auto",
            "panel s obsahem v railu roste — dynamické sdílení výšky Jednání/Novinky přes :has");
    }

    [Fact]
    public void DashboardCss_ShouldProvideResponsiveFallback()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().Contain("@media (max-width: 1024px)",
            "responsive fallback stacking pod 1024px");
    }

    [Theory]
    [InlineData("PmTracker.Web/Views/Dashboard/_DashboardFocusPanel.cshtml")]
    [InlineData("PmTracker.Web/Views/Dashboard/_DashboardMeetingsPanel.cshtml")]
    [InlineData("PmTracker.Web/Views/Dashboard/_DashboardNewsPanel.cshtml")]
    public void DashboardPanels_ShouldUseZobrazitViceOnly(string relativePath)
    {
        var view = File.ReadAllText(ResolvePath(relativePath));
        view.Should().NotContain("Načíst více",
            $"úprava #4: 'Načíst více' smazáno napříč dashboard panely ({relativePath})");
        view.Should().Contain("Zobrazit více",
            "jediné unified CTA");
    }

    [Fact]
    public void DashboardJs_ShouldNotReferenceLoadMore()
    {
        var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/dashboard.js"));
        js.Should().NotContain("data-dashboard-news-load-more",
            "lazy-load pagination smazána — 'Zobrazit více' routuje na /Dashboard/News");
        js.Should().NotContain("loadMoreButton",
            "load-more JS handler smazán");
    }
}
