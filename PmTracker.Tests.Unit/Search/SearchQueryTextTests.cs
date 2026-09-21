using FluentAssertions;
using PmTracker.Web.Services.Search;

namespace PmTracker.Tests.Unit.Search;

public sealed class SearchQueryTextTests
{
    [Theory]
    [InlineData("  zálohování   serveru  ", new[] { "zálohování", "serveru" })]
    [InlineData("jedno", new[] { "jedno" })]
    public void SplitTerms_OrezeMezeryAZahodiPrazdna(string input, string[] expected)
    {
        SearchQueryText.SplitTerms(input).Should().Equal(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    public void SplitTerms_PodPrahemVraciPrazdno(string? input)
    {
        // Práh 3 znaky se vyhodnocuje nad celým dotazem, ne nad jednotlivými slovy.
        SearchQueryText.SplitTerms(input).Should().BeEmpty();
    }

    [Fact]
    public void SplitTerms_OmezujePocetSlov()
    {
        SearchQueryText.SplitTerms("a b c d e f g h i")
            .Should().HaveCount(SearchQueryText.MaxTerms);
    }

    [Theory]
    [InlineData("50 %", "50 [%]")]
    [InlineData("a_b", "a[_]b")]
    [InlineData("[abc]", "[[]abc]")]
    [InlineData("běžný dotaz", "běžný dotaz")]
    public void EscapeLikePattern_ZneskodniZastupneZnaky(string input, string expected)
    {
        SearchQueryText.EscapeLikePattern(input).Should().Be(expected);
    }

    [Fact]
    public void EscapeLikePattern_ZavorkuEscapujeJakoPrvni()
    {
        // Kdyby se '[' escapovala až po '%', vznikl by z "[%]" nesmysl.
        SearchQueryText.EscapeLikePattern("[%]").Should().Be("[[][%]]");
    }

    [Fact]
    public void ToContainsPattern_ObaliProcenty()
    {
        SearchQueryText.ToContainsPattern("záloha").Should().Be("%záloha%");
    }

    [Fact]
    public void BuildSnippet_VratiDveSlovaPredAPo()
    {
        var snippet = SearchQueryText.BuildSnippet(
            "První druhé třetí záloha čtvrté páté šesté",
            new[] { "záloha" });

        snippet.Should().NotBeNull();
        snippet!.Before.Should().Be("druhé třetí ");
        snippet.Match.Should().Be("záloha");
        snippet.After.Should().Be(" čtvrté páté");
    }

    [Fact]
    public void BuildSnippet_IgnorujeDiakritikuAleVratiOriginalniText()
    {
        // Uživatel píše bez diakritiky, zvýraznit se musí text tak, jak je v datech.
        var snippet = SearchQueryText.BuildSnippet("Dnes proběhlo Zálohování dat", new[] { "zalohovani" });

        snippet.Should().NotBeNull();
        snippet!.Match.Should().Be("Zálohování");
    }

    [Fact]
    public void BuildSnippet_BezShodyVraciNull()
    {
        SearchQueryText.BuildSnippet("Text bez shody", new[] { "xyz" }).Should().BeNull();
    }

    [Fact]
    public void BuildSnippet_PrazdnyHaystackVraciNull()
    {
        SearchQueryText.BuildSnippet(null, new[] { "záloha" }).Should().BeNull();
        SearchQueryText.BuildSnippet("", new[] { "záloha" }).Should().BeNull();
    }

    [Fact]
    public void BuildSnippet_NaZacatkuTextuNepada()
    {
        var snippet = SearchQueryText.BuildSnippet("Záloha proběhla dnes večer", new[] { "záloha" });

        snippet.Should().NotBeNull();
        snippet!.Before.Should().BeEmpty();
        snippet.Match.Should().Be("Záloha");
        snippet.After.Should().Be(" proběhla dnes");
    }

    [Theory]
    // Písmena s háčkem. Čeština je bere jako samostatná písmena abecedy, ne jako
    // diakritickou variantu — kdo na to zapomene, tomu „rizeni" nenajde „Řízení".
    [InlineData("Řízení projektu", "rizeni", "Řízení")]
    [InlineData("Číslo jednání", "cislo", "Číslo")]
    [InlineData("Šedý pruh", "sedy", "Šedý")]
    [InlineData("Žlutý stav", "zluty", "Žlutý")]
    // Zbytek diakritiky, který fungoval vždycky.
    [InlineData("Tělo zprávy", "telo", "Tělo")]
    [InlineData("Ďábel", "dabel", "Ďábel")]
    [InlineData("Zálohování dat", "zalohovani", "Zálohování")]
    [InlineData("Půlnoc", "pulnoc", "Půlnoc")]
    [InlineData("Výzva", "vyzva", "Výzva")]
    public void BuildSnippet_NajdeCeskaPismenaBezDiakritiky(string text, string dotaz, string ocekavanaShoda)
    {
        // Tenhle výčet musí souhlasit se SearchQueryText.AccentInsensitiveCollation.
        // Kdyby se .NET a SQL rozešly, databáze by řádek vrátila, ale zvýraznit by
        // ho nešlo — uživatel by viděl výsledek bez žlutého podbarvení.
        var snippet = SearchQueryText.BuildSnippet(text, new[] { dotaz });

        snippet.Should().NotBeNull($"'{dotaz}' se v '{text}' vyskytuje, jen bez diakritiky");
        snippet!.Match.Should().Be(ocekavanaShoda, "zvýrazňuje se text z dat, ne dotaz uživatele");
    }

    [Fact]
    public void AccentInsensitiveCollation_MusiSkladatIPismenaSHackem()
    {
        // Ověřeno dotazem na SQL Server: Czech_CI_AI ani Czech_100_CI_AI NEskládají
        // č/ř/š/ž — v české abecedě mají vlastní primární váhu, takže je akcent-
        // necitlivost minout nemůže. Latin1_General_CI_AI je skládá a zároveň drží
        // á→a, ě→e, ď→d, ů→u, ý→y. .NET protějšek je InvariantCulture.
        SearchQueryText.AccentInsensitiveCollation.Should().Be("Latin1_General_CI_AI");
    }

    [Fact]
    public void BuildSnippet_PouzijePrvniSlovoKtereSePotka()
    {
        var snippet = SearchQueryText.BuildSnippet("alfa beta gama", new[] { "nenajde", "gama" });

        snippet.Should().NotBeNull();
        snippet!.Match.Should().Be("gama");
    }
}
