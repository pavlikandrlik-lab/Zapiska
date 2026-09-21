using FluentAssertions;
using PmTracker.Web.Services.Search;

namespace PmTracker.Tests.Unit.Search;

public sealed class SearchResultModelsTests
{
    private static SearchResultItem Item(int id) => new(
        ZaznamId: id,
        ProjektId: 10,
        Nazev: $"Záznam {id}",
        CisloViditelne: $"RU {id}",
        SubsystemKod: "R_EIS",
        MatchKind: SearchMatchKind.Nazev,
        Snippet: null,
        CisloJednani: null,
        DetailUrl: $"/Projekty/Detail/10?recordId={id}");

    [Fact]
    public void Empty_NemaZadneKategorieAniVysledky()
    {
        var result = SearchResult.Empty("zal");

        result.Query.Should().Be("zal");
        result.Categories.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public void TotalCount_SectePolozkyPresVsechnyKategorie()
    {
        var result = new SearchResult("zal",
        [
            new SearchResultCategory(SearchCategoryKeys.Zaznamy, "Záznamy", [Item(1), Item(2)]),
            new SearchResultCategory("budouci", "Budoucí kategorie", [Item(3)])
        ]);

        result.TotalCount.Should().Be(3,
            "počet se musí odvozovat z kategorií, aby přidání další nevyžadovalo zásah");
    }

    [Fact]
    public void KlicKategorieZaznamy_JeStabilni()
    {
        // Klíč jde do JSON pro dropdown i do markupu stránky — nesmí se měnit náhodou.
        SearchCategoryKeys.Zaznamy.Should().Be("zaznamy");
    }
}
