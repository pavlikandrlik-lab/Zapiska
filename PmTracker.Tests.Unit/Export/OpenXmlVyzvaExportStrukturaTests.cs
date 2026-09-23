using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels.Vyzvy;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Word výzvy podle finálního vzoru (spec 2026-09-10 část B). Kotví se na strukturu dokumentu —
/// pořadí, tabulky, buňky, číslování — ne na vzhled.
/// Čtyři požadavky pokrývají všechny tvary kalkulace: I. jen činnosti, II. jen licence,
/// III. obojí, IV. bez akceptované kalkulace.
/// </summary>
public sealed class OpenXmlVyzvaExportStrukturaTests
{
    private static VyzvaExportKalkulaceRadekViewModel Radek(string kod, string nazev, decimal? cena) => new()
    {
        Kod = kod, Nazev = nazev,
        Rozsah = cena.HasValue ? 1m : null, Sazba = cena,
        CenaBezDph = cena, CenaDph = cena * 0.21m, CenaSDph = cena * 1.21m,
    };

    private static VyzvaExportKalkulaceRadekViewModel[] Radky(decimal? cena) => new[]
    {
        Radek("A", "Analýza", cena), Radek("B", "Programové úpravy", cena),
        Radek("C", "Testování", cena), Radek("D", "Implementace", cena),
    };

    private static VyzvaExportLicenceRadekViewModel[] Licence() => new[]
    {
        new VyzvaExportLicenceRadekViewModel
        {
            Kod = 1, Nazev = "XRG – RSS Rozhraní", CenaBezDph = 94470m, CenaDph = 19838.70m, CenaSDph = 114308.70m,
        },
    };

    private static VyzvaExportPozadavekViewModel Pozadavek(
        string poradi, string htl, bool cinnosti, bool licence, string? html = null) => new()
    {
        PoradoveOznaceni = poradi,
        ZaznamId = 1,
        CisloUkoluVp = "RU867-5",
        Nazev = $"Pozadavek {htl}",
        CisloHtl = htl,
        PozadavekHtml = html,
        Kalkulace = new VyzvaExportKalkulaceViewModel
        {
            Radky = Radky(cinnosti ? 100m : null),
            CelkemBezDph = cinnosti ? 400m : 0m,
            CelkemDph = cinnosti ? 84m : 0m,
            CelkemSDph = cinnosti ? 484m : 0m,
            MaCinnosti = cinnosti,
            MaLicenci = licence,
            LicenceRadky = licence ? Licence() : Array.Empty<VyzvaExportLicenceRadekViewModel>(),
            CenaLicence = licence ? 94470m : null,
        },
    };

    private static VyzvaExportViewModel Model(params VyzvaExportPozadavekViewModel[] pozadavky) => new()
    {
        VyzvaId = 10,
        ProjektId = 1,
        KodVyzvy = "8/2026",
        PoradoveVRoce = 8,
        Rok = 2026,
        CisloRamcoveSmlouvy = "23106000271",
        MistoPlneni = "FIS (EIS): VZ 8201, Tychonova 1, 160 01 Praha 6",
        InformacniSystem = "FIS",
        Pozadavky = pozadavky,
        CelkemBezDph = pozadavky.Sum(p => p.Kalkulace.CelkemBezDph),
        LicenceBezDph = pozadavky.Sum(p => p.Kalkulace.CenaLicence ?? 0m),
    };

    private static VyzvaExportViewModel VsechnyTvary(string? html = null) => Model(
        Pozadavek("I.", "358333", cinnosti: true, licence: false, html),
        Pozadavek("II.", "358310", cinnosti: false, licence: true),
        Pozadavek("III.", "361652", cinnosti: true, licence: true),
        Pozadavek("IV.", "364451", cinnosti: false, licence: false));

    private static WordprocessingDocument Otevrit(VyzvaExportViewModel model)
        => WordprocessingDocument.Open(
            new MemoryStream(new OpenXmlVyzvaExportService().BuildDocument(model), writable: false), false);

    private static string Druh(Table t)
    {
        var hlavicka = t.Elements<TableRow>().First().InnerText;
        if (hlavicka.Contains("Rozsah [hod]")) return "cinnosti";
        if (hlavicka.Contains("PPOL")) return "licence";
        return "jina";
    }

    private static string[] Bunky(TableRow r) => r.Elements<TableCell>().Select(c => c.InnerText).ToArray();

    [Fact]
    public void Stranka_A4_ZahlaviSPrilohou_ZapatiSCislemStranky()
    {
        using var doc = Otevrit(VsechnyTvary());
        var main = doc.MainDocumentPart!;
        var body = main.Document.Body!;

        var sekce = body.Elements<SectionProperties>().Should().ContainSingle().Which;
        sekce.GetFirstChild<PageSize>()!.Width!.Value.Should().Be(11906U, "A4");
        sekce.GetFirstChild<PageMargin>()!.Left!.Value.Should().Be(1417U, "okraj 2,5 cm");

        main.HeaderParts.Should().ContainSingle().Which.Header.InnerText.Should().Contain("Příloha č.1 k Čj. MO");
        body.Elements<Paragraph>().First().InnerText.Should().NotContain("Příloha", "záhlaví patří do záhlaví stránky");
        main.FooterParts.Should().ContainSingle().Which.Footer.Descendants<SimpleField>()
            .Should().Contain(f => f.Instruction!.Value!.Contains("PAGE"), "zápatí nese číslo stránky");
    }

    [Fact]
    public void Pismo_Telo12Bodu_Tabulky10Bodu()
    {
        using var doc = Otevrit(VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;

        body.Elements<Paragraph>().Single(p => p.InnerText.StartsWith("Podrobné návrhy požadavků"))
            .Descendants<Run>().Should().OnlyContain(r => r.RunProperties!.FontSize!.Val == "24", "tělo 12 b.");
        body.Elements<Table>().First().Descendants<Run>()
            .Should().OnlyContain(r => r.RunProperties!.FontSize!.Val == "20", "tabulky 10 b.");
    }

    [Fact]
    public void Uvod_OpravenyZakon_JmenoReditele_TucnyNazevZakazky()
    {
        using var doc = Otevrit(VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;

        body.InnerText.Should().Contain("o zadávání veřejných zakázek,");
        body.InnerText.Should().NotContain("zakázkách", "chyba vzoru se opravuje (spec B9)");
        body.InnerText.Should().Contain("Ing. Petrem ZÁBORCEM");
        body.Descendants<Run>().Single(r => r.InnerText == "Technické zhodnocení APV a DZ")
            .RunProperties!.Bold.Should().NotBeNull();
        body.InnerText.Should().NotContain("XXXX", "prázdná pole zůstávají prázdná, ne zástupná");
    }

    [Fact]
    public void Sekce1_RimskaPoradi_PrefixRU_StrucnePopisyBezVazbyNaPmp()
    {
        using var doc = Otevrit(VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;

        var prvni = Bunky(body.Elements<Table>().First().Elements<TableRow>().ElementAt(1));
        prvni[0].Should().Be("I.");
        prvni[1].Should().Be("RU867-5");

        var texty = body.Elements<Paragraph>().Select(p => p.InnerText).ToList();
        texty.IndexOf("Stručné popisy požadavků:").Should().BeLessThan(
            texty.IndexOf("Počet člověkohodin s rozčleněním dle sazeb a informačních systémů"),
            "stručné popisy patří do sekce 1");
        texty.Should().Contain("Číslo úkolu VP EIS: RU867-5.");
        texty.Should().Contain("Bližší podrobnosti jsou uvedeny v PNF 358333.");
        body.InnerText.Should().NotContain("Vazba na PMP", "rozhodnutí uživatele 2026-09-10");
    }

    [Fact]
    public void NadpisyPozadavku_JsouDveRimskeRadyWordu()
    {
        using var doc = Otevrit(VsechnyTvary());
        var main = doc.MainDocumentPart!;

        main.NumberingDefinitionsPart!.Numbering.Descendants<NumberingFormat>()
            .Should().Contain(f => f.Val! == NumberFormatValues.UpperRoman);

        var nadpisy = main.Document.Body!.Elements<Paragraph>()
            .Where(p => p.InnerText == "Pozadavek 358333").ToList();
        nadpisy.Should().HaveCount(2, "název požadavku je nadpisem v sekci 1 i v sekci 2");
        var cislovani = nadpisy.Select(p => p.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value).ToList();
        cislovani[0].Should().NotBe(cislovani[1], "každá sekce má vlastní řadu od I.");
    }

    [Fact]
    public void Sekce2_TabulkyPodleKalkulace_CinnostiPredLicenci()
    {
        using var doc = Otevrit(VsechnyTvary());
        var druhy = doc.MainDocumentPart!.Document.Body!.Elements<Table>().Select(Druh)
            .Where(d => d != "jina").ToList();

        druhy.Should().Equal("cinnosti", "licence", "cinnosti", "licence");
    }

    [Fact]
    public void TabulkaCinnosti_CelkemVeCtvrteBunce()
    {
        using var doc = Otevrit(VsechnyTvary());
        var tabulka = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First(t => Druh(t) == "cinnosti");

        var soucet = Bunky(tabulka.Elements<TableRow>().Last());
        soucet.Take(3).Should().OnlyContain(b => b.Length == 0);
        soucet[3].Should().Be("CELKEM");
    }

    [Fact]
    public void TabulkaLicenci_PolAPpolPrazdne_CelkemPresCtyriSloupce()
    {
        using var doc = Otevrit(VsechnyTvary());
        var tabulka = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First(t => Druh(t) == "licence");

        var radek = Bunky(tabulka.Elements<TableRow>().ElementAt(1));
        radek[0].Should().Be("1");
        radek[1].Should().Be("XRG – RSS Rozhraní");
        radek[2].Should().BeEmpty("POL doplňuje uživatel ručně");
        radek[3].Should().BeEmpty("PPOL doplňuje uživatel ručně");

        var soucet = tabulka.Elements<TableRow>().Last().Elements<TableCell>().First();
        soucet.InnerText.Should().Be("CELKEM licenční rozšíření");
        soucet.TableCellProperties!.GridSpan!.Val!.Value.Should().Be(4);
    }

    [Fact]
    public void Sekce3_RekapitulaceJenSeSvymiPozadavky_PrazdnyRadekPredSoucty()
    {
        using var doc = Otevrit(VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;
        var rekapitulace = body.Elements<Table>()
            .Where(t => Bunky(t.Elements<TableRow>().First()).SequenceEqual(new[] { "Poř. č.", "Název požadavku", "Cena [Kč]" }))
            .ToList();
        rekapitulace.Should().HaveCount(2, "individuální úpravy a licenční rozšíření");

        var cinnosti = rekapitulace[0].Elements<TableRow>().Select(Bunky).ToList();
        // Jen požadavky s činnostmi, pořadí převzaté ze sekce 1, prázdný řádek před součty.
        // (Equal nemá variantu s důvodem — řetězec by se bral jako další očekávaný prvek.)
        cinnosti.Select(r => r[0]).Should().Equal("Poř. č.", "I.", "III.", "", "Celkem", "", "");
        cinnosti[3].Should().OnlyContain(b => b.Length == 0);

        rekapitulace[1].Elements<TableRow>().Select(r => Bunky(r)[0])
            .Should().Equal("Poř. č.", "II.", "III.", "", "Celkem", "", "");

        body.Elements<Table>().Last(t => Druh(t) == "jina" && Bunky(t.Elements<TableRow>().First())[0] == "Název")
            .Elements<TableRow>().First().InnerText.Should().Contain("Položková cena DPH 21 % [Kč]");
    }

    [Fact]
    public void SeznamyVTextuPozadavku_JsouSeznamyWordu()
    {
        using var doc = Otevrit(VsechnyTvary("<ul><li>prvni</li></ul><ol><li>jedna</li></ol>"));
        var odstavce = doc.MainDocumentPart!.Document.Body!.Elements<Paragraph>().ToList();

        var odrazka = odstavce.Single(p => p.InnerText.Contains("prvni"));
        var cislo = odstavce.Single(p => p.InnerText.Contains("jedna"));

        odrazka.InnerText.Should().NotContain("•", "odrážku kreslí Word, ne znak v textu");
        cislo.InnerText.Should().NotStartWith("1.", "číslo kreslí Word");
        odrazka.ParagraphProperties!.NumberingProperties.Should().NotBeNull();
        cislo.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value
            .Should().NotBe(odrazka.ParagraphProperties.NumberingProperties!.NumberingId!.Val!.Value);
    }

    [Fact]
    public void Zaver_TerminPrazdny_PodpisySeJmeny_TriZalomeniStranky()
    {
        using var doc = Otevrit(VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;

        body.Elements<Paragraph>().Single(p => p.InnerText.StartsWith("Termín pro splnění"))
            .InnerText.Trim().Should().Be("Termín pro splnění dílčí veřejné zakázky do:", "komentář autora: nechat volné");
        body.InnerText.Should().Contain("Ing. Petr ZÁBOREC").And.Contain("Ing. Břetislav MOC")
            .And.Contain("předseda správní rady");
        body.Descendants<Break>().Count(b => b.Type is not null && b.Type.Value == BreakValues.Page)
            .Should().Be(3, "zalomení před sekcemi 2, 3 a 4");
    }

    /// <summary>Dokument musí projít validací schématu Office — Word toleruje víc, než by měl.</summary>
    [Fact]
    public void Dokument_ProjdeValidaciSchematu()
    {
        using var doc = Otevrit(VsechnyTvary("<ul><li>a</li></ul><ol><li>b</li></ol>"));

        var chyby = new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(doc)
            .Select(c => $"{c.Path?.XPath}: {c.Description}")
            .ToList();

        chyby.Should().BeEmpty();
    }
}
