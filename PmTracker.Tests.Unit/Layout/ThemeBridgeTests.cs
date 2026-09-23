using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Motiv obsluhuje nativní gov-theme-switch; aplikace přidává jen roční cookie a výchozí
/// stav podle systému (spec 2026-09-23 §9.1, §12.4).
/// </summary>
public sealed class ThemeBridgeTests
{
    private static string ThemeJs() => File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/theme.js"));
    private static string Layout() => File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_Layout.cshtml"));

    [Fact]
    public void Most_PoslouchaGovChange_JenZPrepinaceMotivu()
    {
        var js = ThemeJs();
        js.Should().Contain("document.addEventListener(\"gov-change\"",
            "gov-change bublá; přepínač se může překreslit, delegace na document přežije");
        js.Should().Contain("detail.component !== govThemeSwitchComponent",
            "gov-change posílá i gov-dropdown a formuláře");
        js.Should().Contain("\"pmtracker.theme.mode\"");
    }

    [Fact]
    public void Most_NeobsahujeTristavovouLogiku()
    {
        var js = ThemeJs();
        js.Should().NotContain("localStorage");
        js.Should().NotContain("data-theme-mode");
        js.Should().NotContain("matchMedia", "výchozí stav podle systému řeší inline skript v <head>");
        js.Should().NotContain("[data-theme-switch]", "most nezávisí na obalu přepínače");
    }

    [Fact]
    public void Layout_DosazujeMotivPodleSystemu_PredPrvnimCss()
    {
        var layout = Layout();
        var resolver = layout.IndexOf("prefers-color-scheme: dark", StringComparison.Ordinal);
        var firstCss = layout.IndexOf("<link rel=\"stylesheet\"", StringComparison.Ordinal);

        resolver.Should().BeGreaterThan(0);
        resolver.Should().BeLessThan(firstCss, "motiv musí být na <html> dřív, než se vykreslí CSS");
        layout.Should().NotContain("data-theme-mode");
    }
}
