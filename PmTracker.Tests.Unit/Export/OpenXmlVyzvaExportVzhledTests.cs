using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Vzhled Wordu výzvy podle vzoru <c>2027xxxx_N_8201_Vyzva_c_x_2027_EIS.docx</c> a připomínek
/// z ručního testu 2026-10-06/07: co je ve vzoru vidět, musí být ve vygenerovaném Wordu stejné —
/// tabulky, písma, odsazení, prázdné řádky, podpisy, zápatí. Mezery jsou ve twipech (1/20 bodu);
/// prázdný řádek Times New Roman 12 b. je zhruba 276.
/// </summary>
public sealed class OpenXmlVyzvaExportVzhledTests
{
    private const int PrazdnyRadek = 276;

    private static int Po(OpenXmlElement? e)
        => e is Paragraph p ? int.Parse(p.ParagraphProperties?.SpacingBetweenLines?.After?.Value ?? "0") : 0;

    private static List<Paragraph> Odstavce(Body body) => body.Elements<Paragraph>().ToList();

    private static bool Tucne(TableCell c) => c.Descendants<Run>().All(r => r.RunProperties?.Bold != null);

    private static bool JePrazdnyOdstavec(OpenXmlElement? e) => e is Paragraph p && p.InnerText.Length == 0;

    [Fact]
    public void HlavickaUradu_NaStredSProlozenimACarouPodAdresou()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var hlavicka = Odstavce(doc.MainDocumentPart!.Document.Body!).Take(3).ToList();

        hlavicka.Select(p => p.InnerText).Should().Equal(
            "Sekce vyzbrojování a akvizic Ministerstva obrany",
            "odbor komunikačních a informačních systémů",
            "náměstí Svobody 471/4, Praha 6, PSČ 160 01, datová schránka hjyaavk");
        hlavicka.Should().OnlyContain(
            p => p.ParagraphProperties!.Justification!.Val! == JustificationValues.Center,
            "vzor má hlavičku úřadu na střed, ne vlevo");
        hlavicka[0].Descendants<Run>().Single().RunProperties!.GetFirstChild<Spacing>()!.Val!.Value
            .Should().Be(40, "první řádek vzoru je proložený o 2 body");

        var cara = hlavicka[2].ParagraphProperties!.ParagraphBorders!.BottomBorder!;
        cara.Val!.Value.Should().Be(BorderValues.Single, "pod adresou je ve vzoru čára přes celou šířku");
        cara.Size!.Value.Should().Be(12U);
    }

    [Fact]
    public void RadekCj_MaZarazkuJakoVzor_ADokumentVychoziTabulatory708()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var main = doc.MainDocumentPart!;

        var cj = Odstavce(main.Document.Body!).Single(p => p.InnerText.StartsWith("Čj.", StringComparison.Ordinal));
        cj.ParagraphProperties!.Tabs!.Elements<TabStop>().Should().ContainSingle(t => t.Position! == 5040);
        main.DocumentSettingsPart!.Settings.GetFirstChild<DefaultTabStop>()!.Val!.Value
            .Should().Be(708, "„V Praze dne“ pak stojí na stejném místě jako ve vzoru");
    }

    [Fact]
    public void Styly_TimesNewRoman12_JednoducheRadkovani_Cestina_OkrajeBunek()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var styly = doc.MainDocumentPart!.StyleDefinitionsPart!.Styles!;

        // Bez vlastních stylů dosazuje Word svoje výchozí: čísla bodů a stránky bezpatkovým
        // písmem, řádkování 1,15, text v buňce nalepený na čáru. Hodnoty jsou ze vzoru.
        var pismo = styly.DocDefaults!.RunPropertiesDefault!.RunPropertiesBaseStyle!;
        pismo.RunFonts!.Ascii!.Value.Should().Be("Times New Roman");
        pismo.Languages!.Val!.Value.Should().Be("cs-CZ", "kontrola pravopisu česky");
        styly.DocDefaults.ParagraphPropertiesDefault!.ParagraphPropertiesBaseStyle?.SpacingBetweenLines
            .Should().BeNull("bez mezer a s jednoduchým řádkováním jako ve vzoru");

        var normalni = styly.Elements<Style>().Single(s => s.Type! == StyleValues.Paragraph && s.Default?.Value == true);
        normalni.StyleRunProperties!.FontSize!.Val!.Value.Should().Be("24", "styl Normální vzoru má 12 b.");

        var tabulka = styly.Elements<Style>().Single(s => s.Type! == StyleValues.Table && s.Default?.Value == true);
        var okraje = tabulka.StyleTableProperties!.TableCellMarginDefault!;
        okraje.TableCellLeftMargin!.Width!.Value.Should().Be(108);
        okraje.TableCellRightMargin!.Width!.Value.Should().Be(108);

        styly.Elements<Style>().Select(s => s.StyleId!.Value).Should().Contain(
            new[] { "Default", "Odstavecseseznamem", "paragraph", "normaltextrun", "tabchar", "eop" },
            "styly, na kterých stojí nadpisy sekcí, požadavky, seznamy a podpisy vzoru");
    }

    /// <summary>
    /// Hlavička převzatá 1:1 z XML vzoru (2026-10-07): styl Záhlaví s úrovní osnovy, prázdné
    /// řádky kolem „Čj.“, nadpis výzvy stylem Nadpis 2 a záhlaví stránky stylem Záhlaví.
    /// </summary>
    [Fact]
    public void Hlavicka_JeStrukturouStejnaJakoVzor()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var main = doc.MainDocumentPart!;
        var hlavicka = Odstavce(main.Document.Body!).Take(8).ToList();

        static string? Styl(Paragraph p) => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        static int? Osnova(Paragraph p) => p.ParagraphProperties?.OutlineLevel?.Val?.Value;

        hlavicka.Take(3).Select(Styl).Should().OnlyContain(s => s == "Zhlav", "hlavička úřadu má ve vzoru styl Záhlaví");
        hlavicka.Take(3).Select(Osnova).Should().Equal(0, 0, null);

        hlavicka.Select(p => p.InnerText).Should().Equal(
            "Sekce vyzbrojování a akvizic Ministerstva obrany",
            "odbor komunikačních a informačních systémů",
            "náměstí Svobody 471/4, Praha 6, PSČ 160 01, datová schránka hjyaavk",
            "",
            "Čj.V Praze dne",
            "",
            "Výzva k poskytnutí plnění č. 8/2026 pro FIS",
            "");
        hlavicka.Skip(3).Take(3).Should().OnlyContain(
            p => p.ParagraphProperties!.Tabs!.Elements<TabStop>().Single().Position! == 5040,
            "prázdné řádky kolem „Čj.“ nesou ve vzoru stejnou zarážku");
        Styl(hlavicka[6]).Should().Be("Nadpis2", "nadpis výzvy má ve vzoru styl Nadpis 2");

        var styly = main.StyleDefinitionsPart!.Styles!.Elements<Style>().ToDictionary(s => s.StyleId!.Value!);
        styly["Zhlav"].StyleParagraphProperties!.Tabs!.Elements<TabStop>().Select(t => t.Position!.Value)
            .Should().Equal(4536, 9072);
        styly["Nadpis2"].StyleParagraphProperties!.OutlineLevel!.Val!.Value.Should().Be(1);

        var zahlavi = main.HeaderParts.Single().Header.Elements<Paragraph>().Single();
        Styl(zahlavi).Should().Be("Zhlav");
        zahlavi.ParagraphProperties!.Justification!.Val!.Value.Should().Be(JustificationValues.Right);

        main.Document.Body!.Elements<SectionProperties>().Single().GetFirstChild<DocGrid>()!.LinePitch!.Value
            .Should().Be(360, "mřížka stránky vzoru");
    }

    [Fact]
    public void CislaSekciAPozadavku_JsouTucna()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary("<ol><li>a</li></ol>"));
        var main = doc.MainDocumentPart!;

        // Řady nadpisů: sekce 1.–7. a dvě římské řady požadavků. Seznamy z textu požadavku mají id od 100.
        var nadpisy = Odstavce(main.Document.Body!)
            .Where(p => p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value < 100)
            .ToList();
        nadpisy.Should().HaveCount(7 + 4 + 4, "sedm sekcí a čtyři požadavky v sekci 1 i 2");

        // Word kreslí číslo formátem konce odstavce — ve vzoru je tučný.
        nadpisy.Should().OnlyContain(p => p.ParagraphProperties!.ParagraphMarkRunProperties!.GetFirstChild<Bold>() != null);
    }

    [Fact]
    public void NadpisyPozadavku_RimskaCislaVpravo_OdsazeniJakoVzor()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var main = doc.MainDocumentPart!;
        var numbering = main.NumberingDefinitionsPart!.Numbering;
        var nadpisy = Odstavce(main.Document.Body!).Where(p => p.InnerText == "Pozadavek 358333").ToList();
        nadpisy.Should().HaveCount(2);

        foreach (var nadpis in nadpisy)
        {
            nadpis.ParagraphProperties!.ParagraphStyleId!.Val!.Value.Should().Be("Odstavecseseznamem");
            var numId = nadpis.ParagraphProperties.NumberingProperties!.NumberingId!.Val!.Value;
            var abstraktni = numbering.Elements<NumberingInstance>().Single(n => n.NumberID!.Value == numId).AbstractNumId!.Val!.Value;
            var uroven = numbering.Elements<AbstractNum>().Single(a => a.AbstractNumberId!.Value == abstraktni).Elements<Level>().First();
            uroven.NumberingFormat!.Val!.Value.Should().Be(NumberFormatValues.UpperRoman);
            uroven.LevelJustification!.Val!.Value.Should().Be(LevelJustificationValues.Right,
                "zarovnaná vlevo se „III.“ lepila na text a „VII.“ odskočila na další tabulátor");
        }

        var odsazeni = nadpisy[0].ParagraphProperties!.Indentation!;
        odsazeni.Left!.Value.Should().Be("426", "sekce 1 vzoru");
        odsazeni.Hanging!.Value.Should().Be("284");
        nadpisy[1].ParagraphProperties!.Indentation.Should().BeNull("sekce 2 vzoru bere odsazení z číslování");
    }

    [Fact]
    public void CisloUkolu_KurzivouJakoVzor()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var cisla = Odstavce(doc.MainDocumentPart!.Document.Body!)
            .Where(p => p.InnerText.StartsWith("Číslo úkolu VP EIS", StringComparison.Ordinal)).ToList();

        cisla.Should().HaveCount(8, "čtyři požadavky v sekci 1 i 2");
        cisla.SelectMany(p => p.Descendants<Run>()).Should().OnlyContain(r => r.RunProperties!.Italic != null);
        cisla.Select(p => p.ParagraphProperties!.Indentation!.Left!.Value).Distinct()
            .Should().BeEquivalentTo(new[] { "426", "360" }, "sekce 1 odsazuje 426, sekce 2 360");
    }

    [Fact]
    public void TextPozadavku_OdsazenyDoBloku_PrazdnyRadekMeziOdstavci_SeznamyJakoVzor()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary(
            "<p>prvni odstavec</p><p><br></p><p>druhy odstavec</p><ul><li>odrazka</li></ul><p>treti odstavec</p>"));
        var odstavce = Odstavce(doc.MainDocumentPart!.Document.Body!);
        var od = odstavce.FindIndex(p => p.InnerText == "Číslo úkolu VP EIS: RU867-5.");
        var blok = odstavce.Skip(od).Take(10).ToList();

        blok.Select(p => p.InnerText).Should().Equal(
            "Číslo úkolu VP EIS: RU867-5.", "",
            "prvni odstavec", "",
            "druhy odstavec",
            "odrazka", "",
            "treti odstavec", "",
            "Bližší podrobnosti jsou uvedeny v PNF 358333.");

        var text = blok[2].ParagraphProperties!;
        text.Indentation!.Left!.Value.Should().Be("426");
        text.Justification!.Val!.Value.Should().Be(JustificationValues.Both);

        var odrazka = blok[5].ParagraphProperties!;
        odrazka.ParagraphStyleId!.Val!.Value.Should().Be("Odstavecseseznamem");
        odrazka.Indentation!.Left!.Value.Should().Be("1134");
        odrazka.Indentation.Hanging!.Value.Should().Be("425");
        blok[5].Descendants<Run>().Should().OnlyContain(r => r.RunProperties!.RunFonts!.Ascii! == "Times New Roman",
            "styl seznamu má Calibri, vzor ho v textu přebíjí")
            .And.OnlyContain(r => r.RunProperties!.FontSize!.Val == "24");
    }

    [Fact]
    public void TabulkaPredmetu_BezPodbarveni_SloupceJakoVzor()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var tabulka = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First();
        var hlavicka = tabulka.Elements<TableRow>().First().Elements<TableCell>().ToList();

        hlavicka.Select(c => c.TableCellProperties!.TableCellWidth!.Width!.Value)
            .Should().Equal(new[] { "704", "1276", "6095", "987" }, "popisky sloupců se ve vzoru vejdou na jeden řádek");
        hlavicka.Should().OnlyContain(c => c.TableCellProperties!.Shading == null || c.TableCellProperties.Shading.Fill!.Value == "auto",
            "hlavička tabulky je ve vzoru bez podbarvení");
        hlavicka.SelectMany(c => c.Descendants<Paragraph>())
            .Should().OnlyContain(p => p.ParagraphProperties!.Justification!.Val! == JustificationValues.Center);
    }

    [Fact]
    public void TabulkyKalkulace_SedaHlavicka_Calibri_SloupceJakoVzor()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var tabulky = doc.MainDocumentPart!.Document.Body!.Elements<Table>().ToList();

        var cinnosti = tabulky.First(t => t.InnerText.Contains("Rozsah [hod]"));
        var radky = cinnosti.Elements<TableRow>().ToList();
        radky[0].Elements<TableCell>().Select(c => c.TableCellProperties!.TableCellWidth!.Width!.Value)
            .Should().Equal("846", "1772", "752", "1423", "1423", "1423", "1423");
        radky[0].Elements<TableCell>().Should().OnlyContain(c => c.TableCellProperties!.Shading!.Fill!.Value == "DBDBDB");
        cinnosti.Descendants<Run>().Should().OnlyContain(r => r.RunProperties!.RunFonts!.Ascii! == "Calibri",
            "tabulka činností je ve vzoru převzatá z Excelu v Calibri");
        radky.Last().Elements<TableCell>().Take(3).Should().OnlyContain(
            c => c.TableCellProperties!.TableCellBorders!.LeftBorder!.Val!.Value == BorderValues.Nil
                 && c.TableCellProperties.TableCellBorders.BottomBorder!.Val!.Value == BorderValues.Nil,
            "pod tabulkou vlevo od CELKEM nejsou ve vzoru čáry");

        var licence = tabulky.First(t => t.InnerText.Contains("PPOL"));
        licence.Elements<TableRow>().First().Elements<TableCell>().Select(c => c.TableCellProperties!.TableCellWidth!.Width!.Value)
            .Should().Equal("845", "2274", "850", "851", "1417", "1418", "1388");
        licence.GetFirstChild<TableProperties>()!.TableLayout!.Type!.Value.Should().Be(TableLayoutValues.Fixed);
        licence.Elements<TableRow>().First().Elements<TableCell>()
            .Should().OnlyContain(c => c.TableCellProperties!.Shading!.Fill!.Value == "DBDBDB");
    }

    [Fact]
    public void RekapitulaceASouhrn_SouctyTucneJakoVzor()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var tabulky = doc.MainDocumentPart!.Document.Body!.Elements<Table>().ToList();

        var rekapitulace = tabulky.Where(t => t.Elements<TableRow>().First().InnerText.StartsWith("Poř. č.Název požadavku", StringComparison.Ordinal)).ToList();
        rekapitulace.Should().HaveCount(2);
        foreach (var r in rekapitulace)
        {
            foreach (var radek in r.Elements<TableRow>().TakeLast(3))
            {
                radek.Elements<TableCell>().Skip(1).Should().OnlyContain(c => Tucne(c), "součty a jejich popisky jsou ve vzoru tučně");
            }
        }

        var souhrn = tabulky.Last();
        souhrn.Elements<TableRow>().Skip(1).SelectMany(r => r.Elements<TableCell>().Skip(1))
            .Should().OnlyContain(c => Tucne(c), "částky souhrnu jsou ve vzoru tučně");
    }

    private const string Tecky = "………………………";

    [Fact]
    public void Podpisy_OdstavceSTabulatory_MistoNaPodpisJakoVzor()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;
        var odstavce = Odstavce(body);
        var od = odstavce.FindIndex(p => p.InnerText.StartsWith("Za nabyvatele:", StringComparison.Ordinal));

        body.Elements<Table>().Last().InnerText.Should().NotContain("nabyvatele", "podpisy nejsou ve vzoru tabulka");
        odstavce.Skip(od).Take(10).Select(p => p.InnerText.Trim()).Should().Equal(
            "Za nabyvatele:Za dodavatele:", "", "", "", "", "", "",
            Tecky + Tecky, "Ing. Petr ZÁBORECIng. Břetislav MOC", "ředitelpředseda správní rady");
        odstavce.Skip(od).Take(10).Should().OnlyContain(p => p.ParagraphProperties!.ParagraphStyleId!.Val!.Value == "paragraph");
        odstavce[od].Descendants<Run>().Where(r => r.InnerText.Length > 0).Should().OnlyContain(
            r => r.RunProperties!.FontSize == null, "text podpisů má písmo stylu Normální (Times New Roman 12 b.)");
    }

    [Fact]
    public void PrazdneRadkyJakoVzor_PredSekci1_PredStrucnymiPopisy_VSekci3()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var prvky = doc.MainDocumentPart!.Document.Body!.ChildElements.ToList();

        static bool Prazdny(OpenXmlElement e) => e is Paragraph p && p.InnerText.Length == 0;
        int PrazdnychPred(Func<OpenXmlElement, bool> cil)
        {
            var i = prvky.FindIndex(e => cil(e));
            var pocet = 0;
            while (i - pocet - 1 >= 0 && Prazdny(prvky[i - pocet - 1])) pocet++;
            return pocet;
        }

        bool Text(OpenXmlElement e, string zacatek) => e is Paragraph p && p.InnerText.StartsWith(zacatek, StringComparison.Ordinal);

        // Ve vzoru jeden prázdný řádek; uživatel chtěl před bodem 1 zhruba o řádek víc (2026-10-06).
        PrazdnychPred(e => Text(e, "Popis předmětu dílčí VZ")).Should().Be(1);
        Po(prvky[prvky.FindIndex(e => Text(e, "Popis předmětu dílčí VZ")) - 2]).Should().BeGreaterThanOrEqualTo(PrazdnyRadek);

        PrazdnychPred(e => Text(e, "Stručné popisy požadavků:")).Should().Be(2);
        PrazdnychPred(e => Text(e, "Individuální úpravy:")).Should().Be(1);
        PrazdnychPred(e => Text(e, "Licenční rozšíření:")).Should().Be(1);
        PrazdnychPred(e => e is Paragraph p && p.InnerText == "Licenční rozšíření" && prvky[prvky.IndexOf(p) - 2] is Table)
            .Should().Be(1, "licence pod tabulkou činností téhož požadavku");
    }

    [Fact]
    public void ZalomeniPredSekcemi2Az4_JeVlastnostiNadpisu_BezPrazdnehoOdstavcePred()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;

        // Samostatný odstavec se zalomením se po tabulce končící na konci stránky přesune na další
        // stránku a vyrobí prázdnou stránku se značkou zalomení. Totéž by udělal prázdný řádek.
        body.Descendants<Break>().Should().NotContain(b => b.Type != null && b.Type.Value == BreakValues.Page);
        var nadpisy = Odstavce(body).Where(p => p.ParagraphProperties?.PageBreakBefore != null).ToList();
        nadpisy.Select(p => p.InnerText.Split(' ')[0]).Should().Equal("Počet", "Celková", "Identifikační");
        nadpisy.Should().OnlyContain(p => !JePrazdnyOdstavec(p.PreviousSibling()));
    }

    [Fact]
    public void Motiv_PismoCalibriJakoVzor()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var motiv = doc.MainDocumentPart!.ThemePart!.Theme!;

        // Tabulka licencí má ve vzoru písmo motivu; bez motivu by Word dosadil své (Aptos) a text by se lámal jinak.
        motiv.ThemeElements!.FontScheme!.MinorFont!.LatinFont!.Typeface!.Value.Should().Be("Calibri");
    }

    [Fact]
    public void Zapati_JakoVzor()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var zapati = doc.MainDocumentPart!.FooterParts.Single().Footer;
        var odstavce = zapati.Elements<Paragraph>().ToList();

        odstavce.Should().HaveCount(2, "vzor má pod číslem stránky prázdný odstavec");
        odstavce.Should().OnlyContain(p => p.ParagraphProperties!.ParagraphStyleId!.Val!.Value == "Zpat");
        odstavce[0].ParagraphProperties!.Indentation!.Left!.Value.Should().Be("1080");
        zapati.Descendants<FieldCode>().Should().ContainSingle(f => f.Text.Contains("PAGE"));
        zapati.Descendants<Run>().Should().OnlyContain(r => r.RunProperties!.FontSize!.Val == "20");
    }
}
