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
        // Práh 3 znaky pro celý dotaz.
        SearchQueryText.SplitTerms(input).Should().BeEmpty();
    }

    /// <summary>
    /// 2026-10-08: „stav migrace a dat“ se rozpadlo na hledání písmen — spojka „a“ byla slovem
    /// dotazu, LIKE '%a%' a podsvícení pak označily každé „a“ na kartě. Krátké slovo vedle
    /// delších se nehledá ani nepodsvítí.
    /// </summary>
    [Theory]
    [InlineData("stav migrace a dat", new[] { "stav", "migrace", "dat" })]
    [InlineData("revize v systému", new[] { "revize", "systému" })]
    [InlineData("podklady na jednání", new[] { "podklady", "jednání" })]
    public void SplitTerms_ZahodiSlovaKratsiNezPrah(string input, string[] expected)
    {
        SearchQueryText.SplitTerms(input).Should().Equal(expected);
    }

    [Theory]
    [InlineData("50 %", new[] { "50", "%" })]
    [InlineData("IS SD", new[] { "IS", "SD" })]
    public void SplitTerms_DotazJenZKratkychSlov_HledaSeCely(string input, string[] expected)
    {
        // Bez delšího slova by se nehledalo nic — „50 %“ má dál najít „Čerpání 50 % rozpočtu“.
        SearchQueryText.SplitTerms(input).Should().Equal(expected);
    }

    [Fact]
    public void SplitTerms_OmezujePocetSlov_KratkaSlovaSeNepocitaji()
    {
        SearchQueryText.SplitTerms("a b c d e f jedna dva tri ctyri pet sest sedm")
            .Should().Equal(new[] { "jedna", "dva", "tri", "ctyri", "pet", "sest" },
                "strop počítá jen slova, která se opravdu hledají");
        SearchQueryText.MaxTerms.Should().Be(6);
    }

    /// <summary>
    /// Uživatel 2026-10-08: text v uvozovkách se hledá jako celek (jako Google), slova bez
    /// uvozovek dál jednotlivě. Uvozovky rovné i české, neuzavřená fráze běží do konce dotazu.
    /// </summary>
    [Theory]
    [InlineData("\"stav migrace a dat\"", new[] { "stav migrace a dat" })]
    [InlineData("„stav migrace a dat“", new[] { "stav migrace a dat" })]
    [InlineData("“stav migrace a dat”", new[] { "stav migrace a dat" })]
    [InlineData("\"stav migrace\" a dat", new[] { "stav migrace", "dat" })]
    [InlineData("dat \"stav   migrace\"", new[] { "dat", "stav migrace" })]
    [InlineData("\"stav migrace", new[] { "stav migrace" })]
    [InlineData("\"\" migrace", new[] { "migrace" })]
    [InlineData("\"IS\" migrace a", new[] { "IS", "migrace" })]
    public void SplitTerms_TextVUvozovkachJeJedenCelek(string input, string[] expected)
    {
        SearchQueryText.SplitTerms(input).Should().Equal(expected);
    }

    [Fact]
    public void SplitTerms_FrazeSePocitaDoStropuJakoJednoSlovo()
    {
        SearchQueryText.SplitTerms("\"jedna dva tri\" ctyri pet sest sedm osm devet")
            .Should().Equal(new[] { "jedna dva tri", "ctyri", "pet", "sest", "sedm", "osm" });
    }

    /// <summary>Odkaz hl nese fráze v uvozovkách, aby je detail podsvítil celé.</summary>
    [Fact]
    public void ToHighlightQuery_FrazeVUvozovkach_SlovaSamostatne()
    {
        SearchQueryText.ToHighlightQuery(new[] { "stav migrace a dat", "dat" })
            .Should().Be("\"stav migrace a dat\" dat");
        // Krátký výraz se hledal jen díky uvozovkám (nebo dotazu jen z krátkých slov) —
        // bez nich by ho podsvícení vedle delšího slova vynechalo.
        SearchQueryText.ToHighlightQuery(new[] { "IS", "migrace" })
            .Should().Be("\"IS\" migrace");
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

    [Theory]
    // Čísla tiketů a záznamů — shoda skoro vždy padne doprostřed.
    [InlineData("123456", "1234")]
    [InlineData("123456", "3456")]
    [InlineData("SD-123456", "123456")]
    [InlineData("RU 123/2026", "123")]
    [InlineData("INC0012345", "12345")]
    // Běžný text: LIKE hledá podřetězec, takže i tady je shoda uprostřed slova normální.
    [InlineData("Zálohování", "loho")]
    [InlineData("nezálohovat", "zaloh")]
    public void BuildSnippet_ShodaUprostredSlova_NevkladaMezeryNavic(string text, string dotaz)
    {
        // Když se výřez do textu vejde celý, musí ho složit zpátky znak po znaku.
        // Dřív se skládal ze slov spojených mezerou, takže z „123456" vypadlo
        // „1234 56" a ze „Zálohování" „Zá loho vání".
        var snippet = SearchQueryText.BuildSnippet(text, new[] { dotaz });

        snippet.Should().NotBeNull();
        (snippet!.Before + snippet.Match + snippet.After).Should().Be(text,
            "výřez nesmí do dat vložit mezeru, která tam není");
    }

    [Fact]
    public void BuildSnippet_ZachovaPuvodniOddelovace()
    {
        // Oddělovač mezi slovy patří do Before/After tak, jak je v datech.
        var snippet = SearchQueryText.BuildSnippet("verze 1.2/3 hotova", new[] { "1.2" });

        snippet.Should().NotBeNull();
        (snippet!.Before + snippet.Match + snippet.After).Should().Be("verze 1.2/3 hotova");
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
