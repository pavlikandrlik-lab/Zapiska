using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Focus karty — 3-col layout (2026-04-21).
/// Identity (projekt + stav) | Content (titulek + popis) | Meta (Vlastník/Subsystem/Založeno/Termín).
/// Cíl: využít horizontální prostor na FHD/4K, kde starý stack layout nechával půlku karty prázdnou.
/// Viz docs/superpowers/specs/2026-04-21-focus-profil-layout.md
/// </summary>
public sealed class DashboardFocusCardLayoutTests
{
    [Fact]
    public void FocusListPartial_ShouldHaveIdentityContentAndMetaSections()
    {
        var partial = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/_DashboardFocusList.cshtml"));

        partial.Should().Contain("dashboard-focus-identity",
            "identity sekce drží projekt kód a stav badge ve vlevo sloupci");
        partial.Should().Contain("dashboard-focus-content",
            "content sekce drží titulek + popis ve středním sloupci");
        partial.Should().Contain("dashboard-focus-meta",
            "meta sekce drží Vlastník/Subsystem/Založeno/Termín v pravém sloupci");
    }

    [Fact]
    public void FocusListPartial_MetaShouldUseDefinitionList()
    {
        var partial = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/_DashboardFocusList.cshtml"));

        partial.Should().Contain("<dl class=\"dashboard-focus-meta\"",
            "meta je semantic definition list (dt/dd), ne flex řádek s 'label: value'");
        partial.Should().Contain("<dt>Vlastník</dt>");
        partial.Should().Contain("<dt>Subsystem</dt>");
        partial.Should().Contain("<dt>Založeno</dt>");
        partial.Should().Contain("<dt>Termín</dt>");
    }

    [Fact]
    public void SiteCss_DashboardFocusItem_ShouldUseThreeColumnGrid()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));

        css.Should().MatchRegex(
            @"\.dashboard-focus-item\s*\{[\s\S]*?grid-template-columns\s*:\s*auto\s+minmax\(\s*0\s*,\s*1fr\s*\)\s+minmax\(\s*180px\s*,\s*260px\s*\)",
            "focus item je grid: auto (identity) | minmax(0,1fr) (content) | minmax(180px,260px) (meta)");
    }

    [Fact]
    public void SiteCss_DashboardFocusItem_ShouldStackOnNarrowViewport()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));

        css.Should().MatchRegex(
            @"@media\s*\(\s*max-width:\s*899px\s*\)\s*\{[\s\S]*?\.dashboard-focus-item\s*\{[\s\S]*?grid-template-columns\s*:\s*1fr",
            "pod 900px se karta stackuje na 1 sloupec (tablet/mobile fallback)");
    }

    [Fact]
    public void SiteCss_DashboardItemDescription_ShouldUseLineClamp()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));

        css.Should().MatchRegex(
            @"\.dashboard-item-description\s*\{[\s\S]*?-webkit-line-clamp\s*:\s*2",
            "popis záznamu se zkrátí na max 2 řádky, aby karty měly konzistentní výšku");
    }

    [Fact]
    public void SiteCss_DashboardListPage_ShouldRemoveCapInFluidTier()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));

        css.Should().MatchRegex(
            @"\.app-main--fluid\s+\.dashboard-list-page\s*\{[\s\S]*?max-width\s*:\s*none",
            "ve fluid tier (Dashboard/Focus atd.) se max-width 76rem zruší, aby karty využily celou šířku na FHD/4K");
    }
}
