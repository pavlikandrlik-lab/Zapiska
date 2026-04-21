using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Širokoúhlý layout Fáze 1+2 (2026-04-20): 2-tier width policy.
/// Default 1280px (reading/form) vs fluid (data-heavy) s clamp padding.
/// Content-driven inner constraints řešeno per-komponenta, ne globally.
/// Viz docs/superpowers/specs/2026-04-20-sirokouhly-layout-design.md.
/// </summary>
public sealed class LayoutWidthPolicyTests
{
    [Fact]
    public void SiteCss_DefaultAppMain_ShouldKeep1280pxMaxWidth()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.app-main\s*\{[^}]*max-width\s*:\s*1280px",
            "default .app-main si drží 1280px max-width pro text/form views");
    }

    [Fact]
    public void SiteCss_AppMainFluid_ShouldUseClampPadding()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.app-main--fluid\s*\{[\s\S]*?padding\s*:\s*0\s+clamp\(\s*16px\s*,\s*2vw\s*,\s*48px\s*\)",
            "fluid tier má clamp(16px, 2vw, 48px) horizontal padding (škálování FHD → 4K)");
    }

    [Fact]
    public void SiteCss_AppMainFluid_ShouldRemoveMaxWidthCap()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.app-main--fluid\s*\{[\s\S]*?max-width\s*:\s*none",
            "fluid tier odstraňuje max-width cap pro 4K využití");
    }

    [Fact]
    public void SiteCss_AppMainFluid_ShouldResetDefaultMargin()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.app-main--fluid\s*\{[\s\S]*?margin\s*:\s*0",
            "fluid tier resetuje margin (default 24px auto 48px by zbytečně odsazoval)");
    }

    [Fact]
    public void Layout_BodyClassSwitch_ShouldSupportFluidTier()
    {
        var layout = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_Layout.cshtml"));
        layout.Should().Contain("dashboard-page",
            "Layout switch rozpoznává BodyClass 'dashboard-page' → fluid tier");
        layout.Should().Contain("app-main--fluid",
            "switch přikládá třídu app-main--fluid k main elementu");
    }

    [Fact]
    public void ProjektyDetail_ShouldUseFluidLayout()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/Detail.cshtml"));
        view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Projekty/Detail je data-heavy (5 tabs, karty, grid) → fluid tier");
    }

    [Fact]
    public void ProjektyIndex_ShouldUseFluidLayout()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/Index.cshtml"));
        view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Projekty/Index seznam karet → fluid tier (víc sloupců na 4K)");
    }

    [Fact]
    public void ProjectDashboardIndex_ShouldUseFluidLayout()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/ProjectDashboard/Index.cshtml"));
        view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "ProjectDashboard panely (zaznamy, NES, výzvy, stats) → fluid tier");
    }

    [Fact]
    public void JednaniDetail_ShouldUseFluidLayout()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Jednani/Detail.cshtml"));
        view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Jednani/Detail (zápis + účastníci + úkoly + attendance) → fluid tier");
    }

    [Fact]
    public void SearchIndex_ShouldUseFluidLayout()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Search/Index.cshtml"));
        view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Search/Index hit list napříč entitami → fluid tier");
    }

    [Fact]
    public void DashboardFocus_ShouldUseFluidLayout()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/Focus.cshtml"));
        view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Dashboard/Focus full list záznamů → fluid tier");
    }

    [Fact]
    public void DashboardMeetingsAndNews_ShouldStayNarrow()
    {
        // Úmyslně ponecháno narrow pro reading UX newsfeed / chronological feed.
        // YAGNI override — pokud později user bude chtít wide, změníme tehdy.
        var meetings = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/Meetings.cshtml"));
        var news = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/News.cshtml"));

        meetings.Should().NotContain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Dashboard/Meetings je chronological feed — reading UX, ne data grid");
        news.Should().NotContain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Dashboard/News je newsfeed — reading UX");
    }

    [Fact]
    public void CiselnikyIndex_ShouldUseFluidLayout()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Ciselniky/Index.cshtml"));
        view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Ciselniky/Index seznam tabulek → fluid tier (jednotnost s ostatními data views)");
    }

    [Fact]
    public void CiselnikyDetail_ShouldUseFluidLayout()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Ciselniky/Detail.cshtml"));
        view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Ciselniky/Detail tabulka řádků → fluid tier");
    }

    [Fact]
    public void NastaveniIndex_ShouldUseFluidLayout()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Nastaveni/Index.cshtml"));
        view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Nastaveni/Index master-detail (sidebar + panel) → fluid tier");
    }

    [Fact]
    public void ProfilIndex_ShouldUseFluidLayout()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Profil/Index.cshtml"));
        view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Profil/Index — 4 preference karty potřebují šířku FHD+, jinak se zalamují do 2 řádek");
    }

    [Fact]
    public void SiteCss_ProfileLayout_ShouldUseCompactGrid()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.profile-layout\s*\{[\s\S]*?grid-template-columns\s*:\s*repeat\(\s*auto-fit\s*,\s*minmax\(\s*240px\s*,\s*360px\s*\)",
            "profile-layout používá repeat(auto-fit, minmax(240px, 360px)) → 4 karty vedle sebe na FHD, cap 360px brání obřím kartám na 4K");
    }
}
