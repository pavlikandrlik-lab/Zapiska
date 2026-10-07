using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Vzhled Wordu výzvy podle vzoru <c>2027xxxx_N_8201_Vyzva_c_x_2027_EIS.docx</c> a připomínek
/// z ručního testu 2026-10-06: hlavička úřadu, tučná čísla bodů, mezery, zalomení stránek.
/// Mezery jsou ve twipech (1/20 bodu); prázdný řádek Times New Roman 12 b. je zhruba 276.
/// </summary>
public sealed class OpenXmlVyzvaExportVzhledTests
{
    private const int PrazdnyRadek = 276;

    private static int Pred(Paragraph p) => int.Parse(p.ParagraphProperties?.SpacingBetweenLines?.Before?.Value ?? "0");

    private static int Po(OpenXmlElement? e)
        => e is Paragraph p ? int.Parse(p.ParagraphProperties?.SpacingBetweenLines?.After?.Value ?? "0") : 0;

    /// <summary>Svislá mezera nad odstavcem: „za" předchozího odstavce + „před" tohoto (Word je sčítá).</summary>
    private static int MezeraNad(Paragraph p) => Po(p.PreviousSibling()) + Pred(p);

    private static List<Paragraph> Odstavce(Body body) => body.Elements<Paragraph>().ToList();

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
        var numbering = main.NumberingDefinitionsPart!.Numbering;

        // Řady nadpisů: sekce 1.–7. a dvě římské řady požadavků. Seznamy z textu požadavku mají id od 100.
        var nadpisy = Odstavce(main.Document.Body!)
            .Where(p => p.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value < 100)
            .ToList();
        nadpisy.Should().HaveCount(7 + 4 + 4, "sedm sekcí a čtyři požadavky v sekci 1 i 2");

        // Word kreslí číslo formátem konce odstavce — ve vzoru je tučný.
        nadpisy.Should().OnlyContain(p => p.ParagraphProperties!.ParagraphMarkRunProperties!.GetFirstChild<Bold>() != null);

        var rady = nadpisy.Select(p => p.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value).Distinct();
        foreach (var numId in rady)
        {
            var abstraktni = numbering.Elements<NumberingInstance>().Single(n => n.NumberID!.Value == numId)
                .AbstractNumId!.Val!.Value;
            numbering.Elements<AbstractNum>().Single(a => a.AbstractNumberId!.Value == abstraktni)
                .Elements<Level>().First().NumberingSymbolRunProperties!.GetFirstChild<Bold>()
                .Should().NotBeNull($"řada {numId} má tučná čísla i po úpravě textu nadpisu ve Wordu");
        }
    }

    [Fact]
    public void Mezery_PredSekci1_PredStrucnymiPopisy_AVSekci3()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var odstavce = Odstavce(doc.MainDocumentPart!.Document.Body!);

        var sekce1 = odstavce.Single(p => p.InnerText.StartsWith("Popis předmětu dílčí VZ", StringComparison.Ordinal));
        sekce1.PreviousSibling()!.InnerText.Should().EndWith("na zadání dílčí veřejné zakázky.");
        MezeraNad(sekce1).Should().BeGreaterThanOrEqualTo(360 + PrazdnyRadek, "dřív 18 b., uživatel chce zhruba o řádek víc");

        var strucne = odstavce.Single(p => p.InnerText == "Stručné popisy požadavků:");
        MezeraNad(strucne).Should().BeGreaterThanOrEqualTo(2 * PrazdnyRadek, "ve vzoru dva prázdné řádky");

        var sekce3Individualni = odstavce.Single(p => p.InnerText == "Individuální úpravy:");
        sekce3Individualni.PreviousSibling()!.InnerText.Should().StartWith("Celková cena");
        MezeraNad(sekce3Individualni).Should().BeGreaterThanOrEqualTo(120 + PrazdnyRadek, "dřív jen 6 b. pod nadpisem");

        var sekce3Licence = odstavce.Single(p => p.InnerText == "Licenční rozšíření:");
        sekce3Licence.PreviousSibling().Should().BeOfType<Table>();
        MezeraNad(sekce3Licence).Should().BeGreaterThanOrEqualTo(120 + PrazdnyRadek, "dřív jen 6 b. pod tabulkou");
    }

    [Fact]
    public void Sekce2_LicencniRozsireniPodTabulkouCinnosti_MaMezeruRadku()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var licencePodTabulkou = Odstavce(doc.MainDocumentPart!.Document.Body!)
            .Where(p => p.InnerText == "Licenční rozšíření" && p.PreviousSibling() is Table)
            .ToList();

        licencePodTabulkou.Should().ContainSingle("jen požadavek III. má činnosti i licenci");
        MezeraNad(licencePodTabulkou[0]).Should().BeGreaterThanOrEqualTo(PrazdnyRadek, "dřív jen 6 b. pod tabulkou");
    }

    [Fact]
    public void ZalomeniPredSekcemi2Az4_JeVlastnostiNadpisu_NeOdstavecSeZalomenim()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;

        // Samostatný odstavec se zalomením se po tabulce končící na konci stránky přesune na další
        // stránku a vyrobí prázdnou stránku se značkou zalomení.
        body.Descendants<Break>().Should().NotContain(b => b.Type != null && b.Type.Value == BreakValues.Page);
        Odstavce(body).Where(p => p.ParagraphProperties?.PageBreakBefore != null)
            .Select(p => p.InnerText.Split(' ')[0])
            .Should().Equal("Počet", "Celková", "Identifikační");
    }

    [Fact]
    public void Zapati_CisloStrankyPolemPage_Times10Bodu()
    {
        using var doc = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary());
        var zapati = doc.MainDocumentPart!.FooterParts.Single().Footer;

        zapati.Descendants<FieldCode>().Should().ContainSingle(f => f.Text.Contains("PAGE"));
        zapati.Descendants<Run>().Should().OnlyContain(r => r.RunProperties!.FontSize!.Val == "20",
            "výsledek pole přebírá formát svých běhů (\\* MERGEFORMAT), jinak Word dosadí výchozí písmo");
    }
}
