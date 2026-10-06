using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// &lt;head&gt; načítá DS gov 4.7.0 podle MANUAL B5 bez index.css (spec 2026-09-23 §12.1).
/// </summary>
public sealed class GovHeadLoadOrderTests
{
    private static string Layout() => File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_Layout.cshtml"));

    // Jen skutečně načítané URL (href/src v <link>/<script>) — komentáře v layoutu
    // zmiňují index.css, core.css i ds-fis a nesmí test shodit.
    private static List<string> LoadedAssets() =>
        Regex.Matches(Layout(), "<(?:link|script)[^>]*(?:href|src)=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

    private static readonly string[] GovCssInOrder =
    {
        "~/assets/gov/styles/tokens.css",
        "~/assets/gov/styles/templates-tokens.css",
        "~/assets/gov/styles/styles.css",
        "~/assets/gov/styles/layout.css",
        "~/assets/gov/styles/components.css",
        "~/assets/gov/styles/templates.css",
        "~/assets/gov/styles/animations.css",
        "~/assets/gov/styles/content.css",
        "~/assets/gov/styles/skip-links.css",
        "~/assets/gov/fonts/roboto.css",
    };

    [Fact]
    public void GovCss_VPoradiManualu_APredAplikacnimCss()
    {
        var layout = Layout();
        var positions = GovCssInOrder
            .Select(href => layout.IndexOf($"href=\"{href}\"", StringComparison.Ordinal))
            .ToList();

        positions.Should().NotContain(-1, "všech 10 odkazů (9 CSS ze styles/ + roboto.css) musí být v layoutu");
        positions.Should().BeInAscendingOrder("pořadí dle MANUAL B5");
        layout.IndexOf("href=\"~/css/tokens.css\"", StringComparison.Ordinal)
            .Should().BeGreaterThan(positions.Last(), "aplikační CSS se načítá až po DS");
    }

    [Theory]
    [InlineData("index.css", "přestyluje main/section/fieldset v celé aplikaci (odchylka č. 2)")]
    [InlineData("components/core.css", "odkazuje na neexistující /playground fonty")]
    [InlineData("ds-fis", "nadstavba DS FIS se nepoužívá (bílá hlavička)")]
    [InlineData("lib/gov-design-system", "4.2.9 — dvě jádra by se přela o definice komponent")]
    public void Layout_NenacitaZakazaneAssety(string fragment, string because)
    {
        LoadedAssets().Should().NotContain(asset => asset.Contains(fragment), because);
    }

    [Fact]
    public void KonfiguraceDs_JePredLoaderem_AIkonyMiriNaAplikacniStrom()
    {
        var layout = Layout();
        var config = layout.IndexOf("window.GOV_DS_CONFIG", StringComparison.Ordinal);
        var loader = layout.IndexOf("~/assets/gov/components/core.esm.js", StringComparison.Ordinal);

        config.Should().BeGreaterThan(0);
        loader.Should().BeGreaterThan(config, "loader čte GOV_DS_CONFIG při startu");
        layout.Should().Contain("iconsPath: \"@Url.Content(\"~/assets/icons\")\"",
            "aplikační strom ikon (odchylka č. 3); Url.Content funguje i v podadresáři");
    }

    [Fact]
    public void SkriptySablon_SeInicializujiPoNacteniDom()
    {
        var layout = Layout();
        layout.Should().Contain("~/assets/gov/templates/scripts.js");
        layout.Should().Contain("window.initTemplateScripts()");
    }

    [Fact]
    public void GovAssety_SeVerzuji_ProtozeSeCachujiNatrvalo()
    {
        // /assets/gov/** má Cache-Control immutable — bez ?v= by upgrade DS se stejnými
        // názvy souborů zůstal rok v cache prohlížeče.
        var tags = Regex.Matches(Layout(), "<(?:link|script)[^>]*~/assets/gov/[^>]*>")
            .Select(m => m.Value)
            .ToList();

        tags.Should().HaveCount(12, "9 CSS ze styles/ + roboto.css + core.esm.js + scripts.js");
        tags.Should().OnlyContain(tag => tag.Contains("asp-append-version=\"true\""));
    }

    [Fact]
    public void StaraKnihovna429_UzNeexistuje()
    {
        Directory.Exists(ResolvePath("PmTracker.Web/wwwroot/lib/gov-design-system")).Should().BeFalse(
            "dvě jádra DS by se přela o definice komponent; jediné jádro je assets/gov (4.7.0)");

        var harness = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/pm-modal-harness.html"));
        harness.Should().NotContain("lib/gov-design-system");
    }
}
