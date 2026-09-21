using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Search;

public sealed class SearchDropdownMarkupTests
{
    private static string Js() =>
        File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/global-search.js"));

    private static string Css() =>
        File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));

    [Fact]
    public void Dropdown_HledaAzOdTriZnaku()
    {
        Js().Should().MatchRegex(@"length\s*<\s*3",
            "práh je 3 znaky — musí sedět se serverem (SearchQueryText.MinQueryLength)");
    }

    [Fact]
    public void Dropdown_MaDebounceAAbortController()
    {
        var js = Js();

        js.Should().Contain("AbortController", "předchozí dotaz se musí rušit");
        js.Should().MatchRegex(@"setTimeout\([^)]*,\s*(150|200)\s*\)",
            "debounce 150–200 ms; bez něj by 10 úhozů poslalo 10 dotazů a narazilo na rate limit");
    }

    [Fact]
    public void Dropdown_VykresliDvaRadky()
    {
        var js = Js();

        js.Should().Contain("app-search-item__title");
        js.Should().Contain("app-search-item__snippet");
        js.Should().Contain("app-search-item__meta", "zkratka subsystému a číslo jednání jdou vpravo");
    }

    [Fact]
    public void Dropdown_ZvyrazniShoduBezVkladaniHtmlZeServeru()
    {
        var js = Js();

        js.Should().Contain("app-search-hl", "zvýraznění má vlastní třídu, ne inline styl");
        js.Should().NotContain("innerHTML",
            "text ze serveru se nesmí vkládat jako HTML — skládej uzly přes textContent");
    }

    [Fact]
    public void Dropdown_MaCssProDvousloupcovyLayoutAZvyrazneni()
    {
        var css = Css();

        css.Should().Contain(".app-search-item");
        css.Should().Contain(".app-search-hl");
    }

    [Fact]
    public void Odchylka_JeZdokumentovana()
    {
        // Pravidlo 4 Design systému: jednorázová odchylka se musí poznamenat.
        var doc = File.ReadAllText(ResolvePath("docs/known-issues/ds-fis-odchylky.md"));

        doc.Should().Contain("gov-form-autocomplete");
        doc.Should().Contain("app-search-dropdown");
    }
}
