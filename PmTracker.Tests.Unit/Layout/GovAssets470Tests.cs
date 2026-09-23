using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// DS gov 4.7.0 leží ve wwwroot/assets/gov jako kopie předsestaveného kitu
/// DesignSystem-FIS-v1.0.0/assets/gov beze změn (spec 2026-09-23 §4.1). Aplikace
/// běží offline, takže všechno, co layout načítá, musí ležet lokálně.
/// </summary>
public sealed class GovAssets470Tests
{
    private static string GovRoot => ResolvePath("PmTracker.Web/wwwroot/assets/gov");
    private static string AppIcons => ResolvePath("PmTracker.Web/wwwroot/assets/icons/components");

    // Sada ikon type="components" v kitu 4.7.0 (DesignSystem-FIS-v1.0.0/assets/gov/icons/components).
    // Kit není v gitu, proto seznam napevno.
    private static readonly string[] KitComponentIcons =
    {
        "arrow-down", "arrow-up", "book", "bookmarks", "box-arrow-up-right", "briefcase",
        "caret-right-fill", "check-circle-fill", "check-lg", "chevron-double-left",
        "chevron-double-right", "chevron-down", "chevron-left", "chevron-right", "chevron-up",
        "clock-history", "cookie", "copy", "dash-lg", "download", "envelope", "envelope-fill",
        "exclamation-lg", "exclamation-triangle-fill", "eye", "eye-slash", "facebook",
        "file-earmark", "filetype-jpg", "filetype-pdf", "filetype-png", "filetype-xls", "gear",
        "geo-alt-fill", "house-door-fill", "info", "info-circle", "info-circle-fill", "instagram",
        "lightbulb-fill", "link", "linkedin", "list", "loader", "moon", "person-fill", "plus-lg",
        "quote", "search", "star-fill", "sun", "telephone", "twitter-x", "upload", "x", "x-lg",
        "youtube",
    };

    [Fact]
    public void Loader_Chunky_A_SkriptySablon_JsouLokalne()
    {
        var components = Path.Combine(GovRoot, "components");
        File.Exists(Path.Combine(components, "core.esm.js")).Should().BeTrue("loader komponent");
        Directory.GetFiles(components, "p-*.js").Length.Should().BeGreaterThanOrEqualTo(100,
            "loader dynamicky importuje chunky; kit 4.7.0 jich má 141");
        File.Exists(Path.Combine(GovRoot, "templates", "scripts.js")).Should().BeTrue(
            "templates/scripts.js ovládá hamburger a přetékání hlavní navigace");
    }

    [Theory]
    [InlineData("tokens.css")]
    [InlineData("templates-tokens.css")]
    [InlineData("styles.css")]
    [InlineData("layout.css")]
    [InlineData("components.css")]
    [InlineData("templates.css")]
    [InlineData("animations.css")]
    [InlineData("content.css")]
    [InlineData("skip-links.css")]
    [InlineData("index.css")]
    public void StylyDs_JsouLokalne(string file)
    {
        File.Exists(Path.Combine(GovRoot, "styles", file)).Should().BeTrue(
            $"styles/{file} je součást kopie kitu (index.css se kopíruje, jen nenačítá)");
    }

    [Fact]
    public void RobotoCss_OdkazujeJenNaLokalniFonty()
    {
        var fonts = Path.Combine(GovRoot, "fonts");
        var css = File.ReadAllText(Path.Combine(fonts, "roboto.css"));
        var urls = Regex.Matches(css, @"url\('([^']+)'\)").Select(m => m.Groups[1].Value).ToList();

        urls.Should().NotBeEmpty();
        foreach (var url in urls)
        {
            url.Should().NotContain("/", "font se načítá vedle roboto.css, ne z CDN ani z absolutní cesty");
            File.Exists(Path.Combine(fonts, url)).Should().BeTrue($"font {url} musí ležet vedle roboto.css");
        }
    }

    [Fact]
    public void NadstavbaDsFis_AIkonyKitu_SeNekopiruji()
    {
        Directory.Exists(ResolvePath("PmTracker.Web/wwwroot/assets/ds-fis")).Should().BeFalse(
            "DS FIS nadstavba (tmavě modrá hlavička) se nepoužívá");
        Directory.Exists(Path.Combine(GovRoot, "icons")).Should().BeFalse(
            "ikony zůstávají v aplikačním /assets/icons (spec §12.2)");
    }

    [Fact]
    public void AplikacniIkony_JsouNadmnozinouKitu()
    {
        // iconsPath míří na aplikační strom a gov komponenty si z něj vnitřně berou
        // ikony kitové sady (chevron-down, x-lg, eye-slash…) — žádná nesmí chybět.
        KitComponentIcons.Where(name => !File.Exists(Path.Combine(AppIcons, name + ".svg")))
            .Should().BeEmpty();
    }

    [Fact]
    public void KazdaIkonaPouzitaVAplikaci_Existuje()
    {
        var sources = Directory.GetFiles(ResolvePath("PmTracker.Web/Views"), "*.cshtml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(ResolvePath("PmTracker.Web/wwwroot/js"), "*.js", SearchOption.AllDirectories));

        var used = new SortedSet<string>();
        foreach (var file in sources)
        {
            // pm-icon je TagHelper, který vykreslí gov-icon type="components".
            foreach (Match tag in Regex.Matches(File.ReadAllText(file), @"<(?:gov-icon|pm-icon)\b[^>]*>"))
            {
                var name = Regex.Match(tag.Value, @"\bname=""([a-z0-9-]+)""");
                if (name.Success)
                {
                    used.Add(name.Groups[1].Value);
                }
            }
        }

        used.Should().NotBeEmpty();
        used.Where(name => !File.Exists(Path.Combine(AppIcons, name + ".svg")))
            .Should().BeEmpty("chybějící SVG = neviditelná ikona, gov-icon tiše selže");
    }
}
