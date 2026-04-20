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
}
