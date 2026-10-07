using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// PDF výzvy je převod jejího Wordu do HTML (uživatel 2026-10-07: „dej do stejného formátu i PDF“).
/// Testy ověřují, že převod nese obsah i formát Wordu: stejné odstavce včetně prázdných řádků,
/// číslování, tabulky se šířkami, čarami a podbarvením, tabulátory, řádkování podle metrik písma
/// jako ve Wordu, záhlaví a zápatí na každé stránce.
/// </summary>
public sealed class WordNaHtmlTests
{
    private const string TextSeSeznamy =
        "<p>Úvod <strong>tučně</strong>.</p><ul><li>první</li><li>druhá</li></ul>"
        + "<p>Kroky:</p><ol><li>jedna</li><li>dvě</li></ol><ol><li>znovu</li></ol>";

    private static byte[] Word() => new OpenXmlVyzvaExportService()
        .BuildDocument(VyzvaExportTestModel.VsechnyTvary(TextSeSeznamy));

    private static WordHtmlDokument Prevod() => WordNaHtml.Preved(Word(), "Výzva č. 8/2026");

    private static XElement Telo(WordHtmlDokument dokument)
    {
        var xml = Regex.Replace(dokument.Html, "^<!DOCTYPE html>", string.Empty);
        return XDocument.Parse(xml, LoadOptions.PreserveWhitespace).Root!.Element("body")!;
    }

    private static string Styl(XElement prvek) => (string?)prvek.Attribute("style") ?? string.Empty;

    private static bool JeCislo(XElement prvek) => ((string?)prvek.Attribute("class"))?.Contains("w-cislo") == true;

    /// <summary>Text odstavce bez značky číslování (tu Word kreslí sám, v XML není) a bez tabulátorů.</summary>
    private static string TextBezCisla(XElement odstavec) => string.Concat(odstavec.DescendantNodes()
            .OfType<XText>()
            .Where(t => !t.Ancestors().Any(JeCislo)))
        .Replace("\t", string.Empty);

    private static string? Cislo(XElement odstavec)
        => odstavec.Descendants().FirstOrDefault(JeCislo)?.Value;

    private static IReadOnlyList<XElement> Odstavce(XElement telo) => telo.Descendants("p").ToList();

    private static XElement Odstavec(XElement telo, string zacatek)
        => Odstavce(telo).First(p => TextBezCisla(p).StartsWith(zacatek, StringComparison.Ordinal));

    /// <summary>Číslovaný odstavec (nadpis požadavku, položka seznamu) — stejný text může být i v buňce tabulky.</summary>
    private static XElement Cislovany(XElement telo, string zacatek)
        => Odstavce(telo).First(p => Cislo(p) is not null && TextBezCisla(p).StartsWith(zacatek, StringComparison.Ordinal));

    private static IReadOnlyList<XElement> Tabulky(XElement telo) => telo.Descendants("table").ToList();

    private static double Bod(string styl, string vlastnost)
    {
        var shoda = Regex.Match(styl, $@"(?:^|;){Regex.Escape(vlastnost)}:(?<h>-?[0-9.]+)pt");
        shoda.Success.Should().BeTrue($"styl „{styl}“ má mít {vlastnost} v bodech");
        return double.Parse(shoda.Groups["h"].Value, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Odstavce_StejneJakoVeWordu_VcetnePrazdnychRadku()
    {
        using var word = VyzvaExportTestModel.Otevrit(VyzvaExportTestModel.VsechnyTvary(TextSeSeznamy));
        var wordOdstavce = word.MainDocumentPart!.Document.Body!.Descendants<Paragraph>()
            .Select(p => p.InnerText).ToList();

        var htmlOdstavce = Odstavce(Telo(Prevod())).Select(TextBezCisla).ToList();

        htmlOdstavce.Should().Equal(wordOdstavce,
            "každý odstavec Wordu (i prázdný řádek a odstavec v buňce) je v PDF jeden odstavec se stejným textem");
    }

    [Fact]
    public void Stranka_A4_SOkrajiWordu()
    {
        var html = Prevod().Html;

        html.Should().Contain("@page{size:595.3pt 841.9pt;margin:70.85pt 0 70.85pt 0}",
            "rozměr stránky a horní a dolní okraj 2,5 cm se čtou z oddílu Wordu");
        html.Should().Contain("body{margin:0;padding:0 70.85pt 0 70.85pt;width:453.6pt",
            "boční okraje jsou odsazení těla — prohlížeč by obsah v okraji stránky ořízl, Word do něj "
            + "kreslí čísla zarovnaná vpravo (XVIII.); šířka sloupce textu přesně jako ve Wordu, prohlížeč "
            + "rozměr stránky zaokrouhluje");
    }

    /// <summary>
    /// Word bez nastavení v dokumentu hlídá osamocené řádky (změřeno: dvouřádkovou položku seznamu
    /// přesunul celou na další stránku, i když se první řádek vešel).
    /// </summary>
    [Fact]
    public void OsamoceneRadky_HlidaJakoWord()
        => Prevod().Html.Should().Contain("p{margin:0;white-space:pre-wrap;orphans:2;widows:2}");

    /// <summary>
    /// Odstavec o třech řádcích Word při hlídání osamocených řádků nerozdělí (2+1 ani 1+2 nejde) a přesune
    /// ho celý. Prohlížeč v tom případě widows obětuje a rozdělí 2+1 (změřeno v Chromiu 154) — počet
    /// řádků je známý až po sazbě, proto ho po načtení stránky dopočítá skript.
    /// </summary>
    [Fact]
    public void OdstavecOTrechRadcich_SeNedeliJakoVeWordu()
    {
        var html = Prevod().Html;

        html.Should().Contain("<script>").And.Contain("breakInside='avoid'");
        html.IndexOf("<script>", StringComparison.Ordinal).Should().BeGreaterThan(html.IndexOf("</p>", StringComparison.Ordinal),
            "skript běží až nad vysázenými odstavci");
    }

    [Fact]
    public void Zahlavi_VpravoSeStejnymTextem_ZapatiCisloStranky()
    {
        var dokument = Prevod();

        dokument.ZahlaviSablona.Should().Contain("Příloha č.1 k Čj. MO").And.Contain("text-align:right");
        dokument.ZapatiSablona.Should().Contain("class=\"pageNumber\"", "pole PAGE vyplní prohlížeč při lámání stránek");
        dokument.ZapatiSablona.Should().Contain("text-align:center").And.Contain("font-size:10pt");
    }

    [Fact]
    public void CislaSekciARimskaCislaPozadavku_JakoWord()
    {
        var telo = Telo(Prevod());
        // Číslo sekce je ve vzoru tučné (formát konce odstavce) — tím se liší od číslovaného seznamu v textu.
        var sekce = Odstavce(telo)
            .Where(p => Cislo(p) is { } c && Regex.IsMatch(c, "^[0-9]+\\.$"))
            .Where(p => Styl(p.Descendants().First(JeCislo)).Contains("font-weight:bold")
                        || Styl(p).Contains("font-weight:bold"))
            .ToList();

        sekce.Select(Cislo).Should().Equal("1.", "2.", "3.", "4.", "5.", "6.", "7.");

        var rimska = Odstavce(telo).Select(Cislo).Where(c => c is not null && Regex.IsMatch(c, "^[IVX]+\\.$")).ToList();
        rimska.Should().Equal(new[] { "I.", "II.", "III.", "IV.", "I.", "II.", "III.", "IV." },
            "sekce 1 a sekce 2 číslují požadavky každá od I.");
    }

    [Fact]
    public void SeznamyZTextuPozadavku_OdrazkyACislovaniOdJedne()
    {
        var telo = Telo(Prevod());

        Cislo(Cislovany(telo, "první")).Should().Be("•");
        Cislo(Cislovany(telo, "jedna")).Should().Be("1.");
        Cislo(Cislovany(telo, "dvě")).Should().Be("2.");
        Cislo(Cislovany(telo, "znovu")).Should().Be("1.", "každý seznam začíná ve Wordu od 1 (StartOverride)");
    }

    [Fact]
    public void Odsazeni_ZaWordu()
    {
        var telo = Telo(Prevod());

        var nadpis = Cislovany(telo, "Pozadavek 358333");
        Bod(Styl(nadpis), "padding-left").Should().Be(21.3, "w:ind w:left=426");
        Bod(Styl(nadpis), "text-indent").Should().Be(-14.2, "w:hanging=284");

        var odrazka = Cislovany(telo, "první");
        Bod(Styl(odrazka), "padding-left").Should().Be(56.7, "odrážka vzoru w:left=1134");
        Bod(Styl(odrazka), "text-indent").Should().Be(-21.25, "w:hanging=425");
    }

    [Fact]
    public void Radkovani_PodleMetrikPismaJakoWord()
    {
        var telo = Telo(Prevod());

        Bod(Styl(Odstavec(telo, "Veřejný zadavatel")), "line-height").Should().BeApproximately(13.799, 0.001,
            "jednoduché řádkování Times New Roman 12 b. je ve Wordu 13,8 b.");
        Bod(Styl(Cislovany(telo, "Pozadavek 358333")), "line-height").Should().BeApproximately(15.869, 0.001,
            "styl Odstavec se seznamem má řádkování 1,15 násobku");

        var misto = Cislovany(telo, "první").Elements("span").First();
        Bod(Styl(misto), "padding-top").Should().BeApproximately(1.038, 0.001,
            "odrážka písmem Symbol zvyšuje ve Wordu řádek na 16,9 b., a to nad textem (větší horní dotah písma)");
    }

    /// <summary>
    /// Odrážka vzoru je znak písma Symbol (soukromá oblast Unicode), které prohlížeč nemusí umět.
    /// Kreslí se jako kruh s rozměry znaku z písma Symbol: průměr 0,357 em, 0,103 em nad účařím, posun 0,46 em.
    /// </summary>
    [Fact]
    public void OdrazkaSymbol_KruhSRozmeryZnakuZPismaSymbol()
    {
        var odrazka = Cislovany(Telo(Prevod()), "první").Descendants().First(JeCislo);
        var styl = Styl(odrazka);

        Bod(styl, "width").Should().BeApproximately(4.283, 0.001);
        Bod(styl, "height").Should().BeApproximately(4.283, 0.001);
        Bod(styl, "vertical-align").Should().BeApproximately(1.236, 0.001);
        Bod(styl, "margin-right").Should().BeApproximately(1.236, 0.001, "zbytek posunu znaku za kruhem");
        styl.Should().Contain("border-radius:50%").And.Contain("background-color:#000");
    }

    /// <summary>
    /// Word (od verze 2013) v odstavci do bloku smrští mezery až o čtvrtinu, když se tím vejde další
    /// slovo. Změřeno na 115 řádcích výzvy z dat vzoru: ponechal řádky se smrštěním do 24,8 %,
    /// zalomil od 26,4 %. Prohlížeč mezery nesmršťuje, proto zúžené mezery a roztažení do bloku.
    /// </summary>
    [Fact]
    public void OdstavecDoBloku_MezeryLzeSmrstitOCtvrtinuJakoVeWordu()
    {
        var telo = Telo(Prevod());

        Bod(Styl(Odstavec(telo, "Veřejný zadavatel")), "word-spacing").Should().Be(-0.75,
            "mezera Times New Roman 12 b. je 3 b., čtvrtina 0,75 b.");
        Styl(Odstavec(telo, "k poskytnutí plnění")).Should().NotContain("word-spacing",
            "na střed Word mezery nesmršťuje");
    }

    [Fact]
    public void MezeryOdstavcu_SeSčítajíJakoVeWordu()
    {
        var veta = Odstavec(Telo(Prevod()), "veřejné zakázky");

        Bod(Styl(veta), "margin-bottom").Should().Be(13.8,
            "w:spacing w:after=276; mezera za odstavcem na konci stránky ve Wordu přetéká do okraje — okraj "
            + "prohlížeč na zlomu stránky zahodí stejně (změřeno: „Bližší podrobnosti…“ se ve Wordu vešel)");
        var stitek = Odstavec(Telo(Prevod()), "Individuální úpravy");
        Bod(Styl(stitek), "padding-top").Should().Be(6,
            "mezera před odstavcem je odsazení — s mezerou za předchozím se ve Wordu sčítá, okraje by se slily");
    }

    [Fact]
    public void TabulkaPredmetu_SirkySloupcuZeVzoru_BezPodbarveni()
    {
        var predmet = Tabulky(Telo(Prevod()))[0];

        predmet.Descendants("col").Select(c => Bod(Styl(c), "width")).Should().Equal(35.2, 63.8, 304.75, 49.35);
        predmet.Descendants("td").Should().NotContain(td => Styl(td).Contains("background"),
            "vzor nemá u tabulky předmětu barevnou hlavičku");
        predmet.Descendants("td").Should().OnlyContain(td => Styl(td).Contains("border-top:0.5pt solid #000"));
    }

    /// <summary>
    /// Word: čára leží na hraně mřížky a text buňky má celou šířku sloupce bez okrajů buňky; výška
    /// řádku je bez čar (změřeno: řádek 14,4 b. s čarou 0,5 b. má rozteč 14,9 b.). Prohlížeč počítá
    /// polovinu čáry dovnitř buňky — bez vyrovnání se „Poř. č.“ zalomilo na dva řádky.
    /// </summary>
    [Fact]
    public void BunkaTabulky_SirkaTextuAVyskaRadkuJakoWord()
    {
        var predmet = Tabulky(Telo(Prevod()))[0];
        var hlavicka = predmet.Descendants("tr").First();
        var prvni = hlavicka.Elements("td").First();

        Styl(prvni).Should().Contain("padding:0pt 3.25pt 0pt 3.25pt",
            "okraj buňky 3,5 b. minus polovina čáry 0,5 b. na každé straně");
        Bod(Styl(hlavicka), "height").Should().Be(14.9, "w:trHeight 288 tw = 14,4 b. plus čára 0,5 b.");
    }

    [Fact]
    public void TabulkaCinnosti_HlavickaPodbarvena_Calibri_CelkemBezLevychCar()
    {
        var cinnosti = Tabulky(Telo(Prevod())).First(t => t.Value.Contains("Jednotková sazba"));
        var radky = cinnosti.Descendants("tr").ToList();

        radky[0].Elements("td").Should().OnlyContain(td => Styl(td).Contains("background-color:#DBDBDB"));
        radky[1].Descendants("p").First().ToString().Should().Contain("Calibri");

        var celkem = radky[^1].Elements("td").ToList();
        celkem.Take(3).Should().OnlyContain(td => Styl(td).Contains("border-left:none") && Styl(td).Contains("border-bottom:none"),
            "první tři buňky řádku CELKEM jsou ve vzoru bez čar");
        Styl(radky[^1]).Should().Contain("break-inside:avoid", "řádek se nedělí přes stránku (cantSplit)");
    }

    [Fact]
    public void ZalomeniSekci_NaNoveStranceANadpisDrziSDalsim()
    {
        var telo = Telo(Prevod());

        foreach (var sekce in new[] { "Počet člověkohodin", "Celková cena", "Identifikační údaje" })
        {
            Styl(Odstavec(telo, sekce)).Should().Contain("break-before:page");
        }

        Styl(Odstavec(telo, "Popis předmětu")).Should().Contain("break-after:avoid", "w:keepNext");
    }

    [Fact]
    public void Tabulatory_CjNaVlastniZarazce_PodpisyNaVychozich()
    {
        var telo = Telo(Prevod());

        var datum = Odstavec(telo, "Čj.").Elements("span").Single(s => s.Value == "V Praze dne");
        Bod(Styl(datum), "left").Should().Be(318.6,
            "Word: zarážka 5040, výchozí zarážky před ní neplatí, pak 5664 a 6372 tw");

        var podpisy = Odstavec(telo, "Za nabyvatele");
        podpisy.Value.Should().Contain("\t\t\t\t\t", "tabulátory vysází prohlížeč na výchozích zarážkách");
        Styl(podpisy).Should().Contain("tab-size:35.4pt");
    }

    [Fact]
    public void BehyJenZMezerATabulatoru_NezvysujiRadek()
    {
        var podpisy = Odstavec(Telo(Prevod()), "Za nabyvatele");

        podpisy.Elements("span").Where(s => s.Value.Length > 0 && s.Value.Trim().Length == 0)
            .Should().NotBeEmpty()
            .And.OnlyContain(s => Styl(s).Contains("line-height:0"),
                "změřeno ve Wordu: tabulátory písmem Calibri řádek podpisů nezvětší");
    }

    [Fact]
    public void TucnyTextPozadavku_JakoStrong()
        => Prevod().Html.Should().Contain("<strong>tučně</strong>");

    [Fact]
    public void PrevodZnaVsechnoCoDokumentVyzvyPouziva()
        => WordNaHtml.NeznameVlastnosti(Word()).Should().BeEmpty(
            "nová vlastnost ve Wordu výzvy se musí promítnout i do PDF — doplň ji do převodu");
}
