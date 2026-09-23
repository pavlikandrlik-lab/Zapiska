using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Guard: aplikace musí fungovat offline. DS gov components/core.css odkazuje v @font-face
/// na /playground/build/assets/fonts/… — cesty, které v aplikaci neexistují (každá stránka
/// by házela 20× 404; v 4.2.9 se kvůli tomu upravoval soubor DS). Soubory DS se nově
/// needitují (README kitu, pravidlo 1): core.css se jen nenačítá a písmo jde přes
/// fonts/roboto.css s lokálními woff2 (GovAssets470Tests).
/// </summary>
public sealed class GovFontsOfflineTests
{
    [Fact]
    public void Layout_NenacitaCoreCssSPlaygroundFonty()
    {
        // Kontroluje jen href — komentář v layoutu core.css zmiňuje.
        var layout = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_Layout.cshtml"));
        layout.Should().NotMatchRegex("href=\"[^\"]*components/core\\.css");
        layout.Should().Contain("href=\"~/assets/gov/fonts/roboto.css\"", "písmo DS se načítá z lokálních woff2");
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
    public void NacitaneCssDs_NeodkazujiNaExterniFonty(string file)
    {
        var css = File.ReadAllText(ResolvePath($"PmTracker.Web/wwwroot/assets/gov/styles/{file}"));
        css.Should().NotContain("/playground/build/assets/fonts/");
        css.Should().NotContain("fonts.gstatic.com", "gov fonty se nesmí stahovat z Google CDN");
    }
}
