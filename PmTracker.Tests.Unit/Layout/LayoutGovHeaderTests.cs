using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Po přechodu na gov-header nezůstane mrtvé CSS/JS staré hlavičky, hlavička není přilepená
/// a nic po ní nepočítá s výškou přilepené hlavičky (spec 2026-09-23 §12.5).
/// </summary>
public sealed class LayoutGovHeaderTests
{
    private static string SiteCssBezKomentaru() =>
        Regex.Replace(File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css")), @"/\*.*?\*/", "",
            RegexOptions.Singleline);

    private static string Block(string css, string selector)
    {
        var match = Regex.Match(css, @"(^|\n)" + Regex.Escape(selector) + @"\s*\{[^}]*\}");
        match.Success.Should().BeTrue($"blok {selector} má existovat");
        return match.Value;
    }

    [Theory]
    [InlineData(@"\.app-header(?![\w-])")]
    [InlineData(@"\.app-topbar(?![\w-])")]
    [InlineData(@"\.app-brand(?![\w-])")]
    [InlineData(@"\.app-nav(?![\w-])")]
    [InlineData(@"\.app-nav-link(?![\w-])")]
    [InlineData(@"\.user-menu")]
    [InlineData(@"\.skip-link(?![\w-])")]
    [InlineData(@"\.app-user-tools(?![\w-])")]
    [InlineData(@"\.app-theme-switch(?![\w-])")]
    [InlineData(@"\[data-theme-switch\]")]
    [InlineData(@"\.app-footer")]
    public void SiteCss_NemaSelektoryStareHlavicky(string selectorPattern)
    {
        SiteCssBezKomentaru().Should().NotMatchRegex(selectorPattern);
    }

    [Fact]
    public void Js_NemaObsluhuStarehoMenuUzivatele()
    {
        var js = Directory.GetFiles(ResolvePath("PmTracker.Web/wwwroot/js"), "*.js", SearchOption.AllDirectories)
            .Select(File.ReadAllText);
        js.Should().NotContain(source => source.Contains("initUserMenu") || source.Contains("data-user-menu"));
    }

    [Fact]
    public void VyskaNadObsahem_SeMeriKMain()
    {
        var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/layout/header-height.js"));
        js.Should().Contain("document.getElementById(\"main\")");
        js.Should().NotContain(".app-header");
    }

    [Fact]
    public void DrobeckovaLista_NeniPrilepenaANeprekryvaHlavicku()
    {
        // Lišta leží pod gov-header. S position/z-index by překryla rozbalené menu uživatele.
        var block = Block(SiteCssBezKomentaru(), ".app-breadcrumb-bar");
        block.Should().NotContain("sticky");
        block.Should().NotContain("z-index");
    }

    [Theory]
    [InlineData(".docs-sidebar,\n.docs-toc")]
    [InlineData(".gantt-picker")]
    [InlineData(".ciselniky-sidebar")]
    [InlineData(".settings-sidebar")]
    public void PrilepenePanely_PouzivajiSpolecnyOdsazeni(string selector)
    {
        var block = Block(SiteCssBezKomentaru(), selector);
        block.Should().Contain("top: var(--app-sticky-top)",
            "hlavička už není přilepená — panely se lepí k hornímu okraji okna");
    }

    [Fact]
    public void Kotvy_NepocitajiSVyskouHlavicky()
    {
        var css = SiteCssBezKomentaru();
        css.Should().Contain("scroll-padding-top: var(--app-sticky-top)");
        css.Should().Contain("scroll-margin-top: var(--app-sticky-top)");
        css.Should().NotContain("scroll-padding-top: calc(var(--app-header-h");
        css.Should().NotContain("scroll-margin-top: calc(var(--app-header-h");
    }

    [Fact]
    public void AktivniPolozkaNavigace_MaVTmavemMotivuKontrastniPodtrzeni()
    {
        // Odchylka č. 4: v tmavém motivu má --color-primary-600 proti pozadí navigace
        // (--color-neutral-950) jen ~1,7:1 → podtržení aktivní položky musí přepnout na
        // světlejší token (WCAG 1.4.11 ≥ 3:1).
        var block = Block(SiteCssBezKomentaru(), ":root[data-theme=\"dark\"] .app-main-nav a[aria-current=\"page\"]");
        block.Should().Contain("box-shadow: inset 0 -3px 0 var(--color-primary-400)");
    }

    [Fact]
    public void NadpisyDokumentace_NepocitajiSVyskouHlavicky()
    {
        // Cíle kotev obsahu dokumentace (odkazy z .docs-toc). Dřívějších 92px = výška staré
        // přilepené hlavičky — bez ní by nad nadpisem zůstala prázdná mezera.
        var block = Block(SiteCssBezKomentaru(), ".docs-content h1,\n.docs-content h2,\n.docs-content h3");
        block.Should().Contain("scroll-margin-top: var(--app-sticky-top)");
    }
}
