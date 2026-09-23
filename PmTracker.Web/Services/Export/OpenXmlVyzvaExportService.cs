using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels.Vyzvy;

namespace PmTracker.Web.Services.Export;

/// <summary>Word export výzvy k poskytnutí plnění (spec 2026-09-07 §9).</summary>
public interface IVyzvaWordExportService
{
    byte[] BuildDocument(VyzvaExportViewModel model);
}

/// <summary>
/// Staví .docx výzvy přesně podle finálního vzoru (spec 2026-09-10 část B): A4, Times New
/// Roman 12 v těle a 10 v tabulkách, záhlaví s přílohou, zápatí s číslem stránky, skutečné
/// seznamy Wordu. Čte tutéž projekci jako náhled <c>Views/Export/VyzvaTemplate.cshtml</c> —
/// pořadí a obsah částí musí zůstat shodné. Údaje, které aplikace nemá (č. j., datum, termín),
/// zůstávají prázdné k doplnění ve Wordu.
/// </summary>
public sealed class OpenXmlVyzvaExportService : IVyzvaWordExportService
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const int Telo = 24;            // 12 b. — styl Normal vzoru
    private const int Male = 20;            // 10 b. — tabulky a štítky v sekci 2
    private const int PrvniRadekUradu = 32; // 16 b.
    private const int Podpisy = 18;         // 9 b.
    private const int SirkaTextu = 9072;    // A4 11906 − 2 × okraj 1417 (twipy)

    private const int NumSekce = 1;
    private const int NumPozadavkySekce1 = 2;
    private const int NumPozadavkySekce2 = 3;

    private const string PodrobneNavrhy =
        "Podrobné návrhy požadavků jsou součástí příslušného protokolu (HotLine – uvedená v tabulce shora) "
        + "a specifikace. Byly analyzovány dodavatelem a jejich užitnost je posuzována zadavatelem, vedením "
        + "projektu EIS, Řídícím výborem FIS, případně dalšími odborníky. Požadavky jsou posuzovány jednotlivými "
        + "vedoucími subsystémů FIS/ISSP a příslušnými metodiky. Požadavky jsou schváleny vedením projektu "
        + "FIS/ISSP (VP EIS). Čísla úkolů jsou uvedena také v souhrnné tabulce shora. Dále je uvedena stručná "
        + "anotace požadavků.";

    private static readonly string[] HlavickaCinnosti =
    {
        "Kód činnosti", "Název činnosti", "Rozsah [hod]", "Jednotková sazba bez DPH [Kč]",
        "Položková cena bez DPH [Kč]", "Položková cena DPH [Kč]", "Položková cena s DPH [Kč]",
    };

    private static readonly string[] HlavickaLicenci =
    {
        "Kód činnosti", "Název činnosti", "POL", "PPOL", "Cena v Kč bez DPH", "DPH v Kč", "Cena v Kč s DPH",
    };

    public byte[] BuildDocument(VyzvaExportViewModel model)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body
                ?? throw new InvalidOperationException("Word body nebyl inicializován.");
            var cislovani = new Cislovani();

            AppendUvod(body, model);
            AppendPredmet(body, model, cislovani);
            ZalomitStranku(body);
            AppendClovekohodiny(body, model);
            ZalomitStranku(body);
            AppendCelkovaCena(body, model);
            ZalomitStranku(body);
            AppendZavery(body, model);

            cislovani.Zapsat(mainPart);
            // Vlastnosti oddílu musí být posledním potomkem těla.
            body.Append(Stranka(mainPart));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static string Castka(decimal? c) => (c ?? 0m).ToString("N2", Cs);

    private static string CastkaNeboPrazdno(decimal? c) => c.HasValue ? c.Value.ToString("N2", Cs) : string.Empty;

    private static string Hodiny(decimal? h) => h.HasValue ? h.Value.ToString("0.##", Cs) : string.Empty;

    private static string DphProcenta => (VyzvaExportViewModel.DphSazba * 100m).ToString("0.##", Cs);

    private static Run Beh(string text, bool tucne = false, int velikost = Telo)
        => new(
            OpenXmlWordElements.CreateRunProperties(velikost, tucne, italic: false, strike: false, underline: false),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    /// <summary>Vlastnosti odstavce v pořadí, které vyžaduje schéma (keepNext, numPr, spacing, jc).</summary>
    private static ParagraphProperties Vlastnosti(
        int po = 120, int pred = 0, JustificationValues? zarovnani = null, int? numId = null, bool drzetSDalsim = false)
    {
        var pPr = new ParagraphProperties();
        if (drzetSDalsim) pPr.Append(new KeepNext());
        if (numId.HasValue)
        {
            pPr.Append(new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = numId.Value }));
        }
        pPr.Append(new SpacingBetweenLines { Before = pred.ToString(Inv), After = po.ToString(Inv) });
        if (zarovnani.HasValue) pPr.Append(new Justification { Val = zarovnani.Value });
        return pPr;
    }

    private static Paragraph Odstavec(
        string text, bool tucne = false, int velikost = Telo, JustificationValues? zarovnani = null,
        int po = 120, int pred = 0, int? numId = null, bool drzetSDalsim = false)
        => new(Vlastnosti(po, pred, zarovnani, numId, drzetSDalsim), Beh(text, tucne, velikost));

    /// <summary>Nadpis sekce 1.–7. — číslovaný seznam Wordu arabsky.</summary>
    private static void Nadpis(Body body, string text)
        => body.Append(Odstavec(text, tucne: true, pred: 240, numId: NumSekce, drzetSDalsim: true));

    /// <summary>Nadpis požadavku — římská řada Wordu, v sekci 1 a 2 každá zvlášť od I.</summary>
    private static void NadpisPozadavku(Body body, string? nazev, int numId)
        => body.Append(Odstavec(nazev ?? string.Empty, tucne: true, pred: 180, numId: numId, drzetSDalsim: true));

    private static void ZalomitStranku(Body body)
        => body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));

    private static string CisloUkolu(VyzvaExportPozadavekViewModel p)
        => string.IsNullOrWhiteSpace(p.CisloUkoluVp)
            ? "Číslo úkolu VP EIS:"
            : $"Číslo úkolu VP EIS: {p.CisloUkoluVp}.";

    private static Table Tabulka(int sloupcu, bool ramecek = true)
    {
        // Pořadí dle schématu: tblW před tblBorders.
        var vlastnosti = new TableProperties(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });
        if (ramecek)
        {
            vlastnosti.Append(new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }));
        }

        // Mřížka musí existovat, jinak Word sloučené buňky (gridSpan) rozloží špatně.
        var sirka = (SirkaTextu / sloupcu).ToString(Inv);
        return new Table(
            vlastnosti,
            new TableGrid(Enumerable.Range(0, sloupcu).Select(_ => new GridColumn { Width = sirka })));
    }

    /// <summary>Řádek se nedělí přes stránku (cantSplit) — náhrada ručních zalomení vzoru.</summary>
    private static TableRow Radek(params TableCell[] bunky)
    {
        var radek = new TableRow(new TableRowProperties(new CantSplit()));
        radek.Append(bunky);
        return radek;
    }

    private static TableCell Bunka(string text, bool tucne = false, string? podklad = null, int sloucit = 1, int velikost = Male)
    {
        var tcPr = new TableCellProperties();
        if (sloucit > 1) tcPr.Append(new GridSpan { Val = sloucit });
        if (podklad is not null) tcPr.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = podklad, Color = "auto" });
        return new TableCell(tcPr, Odstavec(text, tucne, velikost, po: 30, pred: 30));
    }

    private static TableRow HlavickaTabulky(params string[] texty)
        => Radek(texty.Select(t => Bunka(t, tucne: true, podklad: "EFEFEF")).ToArray());

    private static TableRow Data(bool tucne, params string[] texty)
        => Radek(texty.Select(t => Bunka(t, tucne)).ToArray());

    private static void AppendUvod(Body body, VyzvaExportViewModel model)
    {
        body.Append(Odstavec("Sekce vyzbrojování a akvizic Ministerstva obrany", tucne: true, velikost: PrvniRadekUradu, po: 0));
        body.Append(Odstavec("odbor komunikačních a informačních systémů", tucne: true, po: 0));
        body.Append(Odstavec("náměstí Svobody 471/4, Praha 6, PSČ 160 01, datová schránka hjyaavk", velikost: Male, po: 240));

        // Č. j. a datum doplní uživatel ve Wordu — vzor je má prázdné (spec B3).
        body.Append(new Paragraph(
            Vlastnosti(po: 240),
            new Run(
                OpenXmlWordElements.CreateRunProperties(Telo, bold: false, italic: false, strike: false, underline: false),
                new Text("Čj."), new TabChar(), new TabChar(), new TabChar(), new Text("V Praze dne"))));

        body.Append(Odstavec(
            $"Výzva k poskytnutí plnění č. {model.KodVyzvy} pro {model.InformacniSystem}",
            tucne: true, zarovnani: JustificationValues.Both, pred: 240, po: 240));

        // Název zákona opravený proti vzoru („zakázkách" → „zakázek", spec B9).
        body.Append(Odstavec(
            "Veřejný zadavatel Česká republika – Ministerstvo obrany, se sídlem Tychonova 1, Praha 6, "
            + "zastoupena ředitelem odboru vyzbrojování pozemních sil a KIS Sekce vyzbrojování a akvizic MO "
            + "Ing. Petrem ZÁBORCEM, se sídlem na adrese náměstí Svobody 471/4, 160 01 Praha 6 "
            + "(dále jen „nabyvatel“), Vás vyzývá podle ustanovení § 134 zákona č. 134/2016 Sb., "
            + "o zadávání veřejných zakázek, ve znění pozdějších předpisů, v souladu s čl. IV. rámcové dohody "
            + $"číslo {model.CisloRamcoveSmlouvy} (dále jen „rámcová dohoda“) a v souladu s podmínkami v ní uvedenými",
            zarovnani: JustificationValues.Both));

        body.Append(Odstavec("k poskytnutí plnění", tucne: true, zarovnani: JustificationValues.Center));

        body.Append(new Paragraph(
            Vlastnosti(zarovnani: JustificationValues.Both),
            Beh("veřejné zakázky „"),
            Beh("Technické zhodnocení APV a DZ", tucne: true),
            Beh("“ pořadové číslo "),
            Beh($"{model.PoradoveVRoce.ToString("00", Cs)}/{model.Rok}", tucne: true),
            Beh(" (dále jen „Výzva“) na zadání dílčí veřejné zakázky.")));
    }

    private static void AppendPredmet(Body body, VyzvaExportViewModel model, Cislovani cislovani)
    {
        Nadpis(body, "Popis předmětu dílčí VZ na základě rámcové dohody, příp. počet dodávaných "
                     + "souvisejících rozšíření licence APV a DZ");

        var table = Tabulka(4);
        table.Append(HlavickaTabulky("Poř. č.", "Č. úkolu VP", "Název požadavku", "Č. HTL"));
        foreach (var p in model.Pozadavky)
        {
            table.Append(Data(false, p.PoradoveOznaceni, p.CisloUkoluVp ?? string.Empty, p.Nazev ?? string.Empty, p.CisloHtl));
        }
        body.Append(table);

        body.Append(Odstavec(PodrobneNavrhy, zarovnani: JustificationValues.Both, pred: 120));
        body.Append(Odstavec("Stručné popisy požadavků:", tucne: true, drzetSDalsim: true));

        foreach (var p in model.Pozadavky)
        {
            NadpisPozadavku(body, p.Nazev, NumPozadavkySekce1);
            body.Append(Odstavec(CisloUkolu(p)));
            if (!string.IsNullOrWhiteSpace(p.PozadavekHtml))
            {
                PozadavekOdstavce(body, p.PozadavekHtml!, cislovani);
            }
            // Vždy „Bližší podrobnosti…", nikdy „Vazba na PMP" (rozhodnutí uživatele 2026-09-10).
            body.Append(Odstavec($"Bližší podrobnosti jsou uvedeny v PNF {p.CisloHtl}."));
        }
    }

    private static void AppendClovekohodiny(Body body, VyzvaExportViewModel model)
    {
        Nadpis(body, "Počet člověkohodin s rozčleněním dle sazeb a informačních systémů");

        foreach (var p in model.Pozadavky)
        {
            var k = p.Kalkulace;
            NadpisPozadavku(body, p.Nazev, NumPozadavkySekce2);
            body.Append(Odstavec(CisloUkolu(p), velikost: Male, drzetSDalsim: k.MaCinnosti || k.MaLicenci));

            // Nejdřív činnosti, pak licence; bez akceptované kalkulace žádná tabulka (spec B5).
            if (k.MaCinnosti)
            {
                body.Append(Odstavec("Individuální úpravy", velikost: Male, drzetSDalsim: true));
                var table = Tabulka(7);
                table.Append(HlavickaTabulky(HlavickaCinnosti));
                foreach (var r in k.Radky)
                {
                    table.Append(Data(false, r.Kod, r.Nazev, Hodiny(r.Rozsah), CastkaNeboPrazdno(r.Sazba),
                        CastkaNeboPrazdno(r.CenaBezDph), CastkaNeboPrazdno(r.CenaDph), CastkaNeboPrazdno(r.CenaSDph)));
                }
                // Vzor: tři prázdné buňky, „CELKEM" ve čtvrté, pak částky.
                table.Append(Data(true, string.Empty, string.Empty, string.Empty, "CELKEM",
                    Castka(k.CelkemBezDph), Castka(k.CelkemDph), Castka(k.CelkemSDph)));
                body.Append(table);
            }

            if (k.MaLicenci)
            {
                body.Append(Odstavec("Licenční rozšíření", velikost: Male, pred: k.MaCinnosti ? 120 : 0, drzetSDalsim: true));
                var table = Tabulka(7);
                table.Append(HlavickaTabulky(HlavickaLicenci));
                foreach (var r in k.LicenceRadky)
                {
                    // POL a PPOL vždy prázdné — v databázi pro ně sloupec není (spec B5).
                    table.Append(Data(false, r.Kod.ToString(Inv), r.Nazev, string.Empty, string.Empty,
                        Castka(r.CenaBezDph), Castka(r.CenaDph), Castka(r.CenaSDph)));
                }
                table.Append(Radek(
                    Bunka("CELKEM licenční rozšíření", tucne: true, sloucit: 4),
                    Bunka(Castka(k.CenaLicence), tucne: true),
                    Bunka(Castka(k.LicenceRadky.Sum(r => r.CenaDph)), tucne: true),
                    Bunka(Castka(k.LicenceRadky.Sum(r => r.CenaSDph)), tucne: true)));
                body.Append(table);
            }
        }
    }

    private static void AppendCelkovaCena(Body body, VyzvaExportViewModel model)
    {
        Nadpis(body, "Celková cena za člověkohodiny, příp. související rozšíření licence APV a DZ "
                     + "IS GINIS® DEFENCE");

        body.Append(Odstavec("Individuální úpravy:", drzetSDalsim: true));
        body.Append(Rekapitulace(
            model.Pozadavky.Where(p => p.Kalkulace.MaCinnosti)
                .Select(p => (p.PoradoveOznaceni, p.Nazev, p.Kalkulace.CelkemBezDph)),
            model.CelkemBezDph, model.CelkemDph, model.CelkemSDph));

        body.Append(Odstavec("Licenční rozšíření:", pred: 120, drzetSDalsim: true));
        body.Append(Rekapitulace(
            model.Pozadavky.Where(p => p.Kalkulace.MaLicenci)
                .Select(p => (p.PoradoveOznaceni, p.Nazev, p.Kalkulace.CenaLicence ?? 0m)),
            model.LicenceBezDph, model.LicenceDph, model.LicenceSDph));

        // Prázdný odstavec mezi tabulkami — sousední tabulky by Word slil do jedné.
        body.Append(new Paragraph(Vlastnosti()));

        var souhrn = Tabulka(4);
        souhrn.Append(HlavickaTabulky("Název", "Položková cena bez DPH [Kč]",
            $"Položková cena DPH {DphProcenta} % [Kč]", "Položková cena s DPH [Kč]"));
        souhrn.Append(Data(false, "Úprava APV a DZ celkem",
            Castka(model.CelkemBezDph), Castka(model.CelkemDph), Castka(model.CelkemSDph)));
        souhrn.Append(Data(false, "Licenční rozšíření APV a DZ celkem",
            Castka(model.LicenceBezDph), Castka(model.LicenceDph), Castka(model.LicenceSDph)));
        souhrn.Append(Data(true, "CELKEM",
            Castka(model.CelkemBezDph + model.LicenceBezDph),
            Castka(model.CelkemDph + model.LicenceDph),
            Castka(model.CelkemSDph + model.LicenceSDph)));
        body.Append(souhrn);
    }

    /// <summary>
    /// Rekapitulace sekce 3: řádky požadavků s pořadím ze sekce 1 (nepřečíslovává se),
    /// prázdný řádek, součty (spec B6). „Poř. č." velkým písmenem — chyba vzoru opravená.
    /// </summary>
    private static Table Rekapitulace(
        IEnumerable<(string Poradi, string? Nazev, decimal Cena)> radky, decimal bez, decimal dph, decimal sDph)
    {
        var table = Tabulka(3);
        table.Append(HlavickaTabulky("Poř. č.", "Název požadavku", "Cena [Kč]"));
        foreach (var (poradi, nazev, cena) in radky)
        {
            table.Append(Data(false, poradi, nazev ?? string.Empty, Castka(cena)));
        }
        table.Append(Data(false, string.Empty, string.Empty, string.Empty));
        table.Append(Data(true, "Celkem", "bez DPH", Castka(bez)));
        table.Append(Data(false, string.Empty, $"DPH {DphProcenta} %", Castka(dph)));
        table.Append(Data(false, string.Empty, "s DPH", Castka(sDph)));
        return table;
    }

    private static void AppendZavery(Body body, VyzvaExportViewModel model)
    {
        Nadpis(body, "Identifikační údaje nabyvatele");
        foreach (var radek in new[]
                 {
                     "Česká republika – Ministerstvo obrany", "Tychonova 1", "160 00 Praha 6",
                     "IČO: 60162694, DIČ: CZ60162694", "v zastoupení", "Sekce vyzbrojování a akvizic MO",
                     "odbor vyzbrojování pozemních sil a komunikačních a informačních systémů",
                     "náměstí Svobody 471/4", "160 01 Praha 6",
                 })
        {
            body.Append(Odstavec(radek, po: 0));
        }

        Nadpis(body, "Termín a místo plnění");
        // Datum „Nechat volné" (komentář autora vzoru) — doplní uživatel ve Wordu.
        body.Append(Odstavec("Termín pro splnění dílčí veřejné zakázky do: "));
        body.Append(Odstavec("Místem plnění je:", po: 0));
        body.Append(Odstavec(model.MistoPlneni, tucne: true));

        Nadpis(body, "Lhůta pro písemné potvrzení Výzvy");
        body.Append(Odstavec("Dodavatel dle čl. IV odst. 1 rámcové smlouvy potvrdí tuto Výzvu do 5 dnů "
                             + "od jejího doručení.", tucne: true));

        Nadpis(body, "Datum a místo potvrzení výzvy dodavatelem");
        // Obě jména ponechal uživatel 2026-09-10 — jsou ve vzoru a s výzvou se nemění.
        var podpisy = Tabulka(2, ramecek: false);
        podpisy.Append(Radek(Bunka("Za nabyvatele:", tucne: true, velikost: Podpisy), Bunka("Za dodavatele:", tucne: true, velikost: Podpisy)));
        podpisy.Append(Radek(Bunka("………………………", velikost: Podpisy), Bunka("………………………", velikost: Podpisy)));
        podpisy.Append(Radek(Bunka("Ing. Petr ZÁBOREC", velikost: Podpisy), Bunka("Ing. Břetislav MOC", velikost: Podpisy)));
        podpisy.Append(Radek(Bunka("ředitel", velikost: Podpisy), Bunka("předseda správní rady", velikost: Podpisy)));
        body.Append(podpisy);
    }

    /// <summary>
    /// Text požadavku jako formátovaný rich text ve 12 bodech. Seznamy jsou skutečné seznamy
    /// Wordu: parser u položky seznamu nese druh a identitu seznamu a první token je značka,
    /// kterou tu přeskakujeme — Word ji kreslí sám (spec B8). Odkazy jako prostý text.
    /// </summary>
    private static void PozadavekOdstavce(Body body, string safeHtml, Cislovani cislovani)
    {
        var seznamy = new Dictionary<int, int>(); // ListId parseru → numId Wordu

        foreach (var odstavec in RichTextHtmlParser.Parse(safeHtml))
        {
            var pPr = new ParagraphProperties();
            IEnumerable<RichTextToken> tokeny = odstavec.Tokens;

            if (odstavec.ListKind != RichTextListKind.None)
            {
                if (!seznamy.TryGetValue(odstavec.ListId, out var numId))
                {
                    numId = cislovani.NovySeznam(odstavec.ListKind == RichTextListKind.Ordered);
                    seznamy[odstavec.ListId] = numId;
                }

                pPr.Append(new NumberingProperties(
                    new NumberingLevelReference { Val = Math.Min(odstavec.IndentLevel, 2) },
                    new NumberingId { Val = numId }));
                tokeny = odstavec.Tokens.Skip(1);
            }

            pPr.Append(new SpacingBetweenLines { After = "120" });
            if (odstavec.ListKind == RichTextListKind.None && odstavec.IndentLevel > 0)
            {
                pPr.Append(new Indentation { Left = (odstavec.IndentLevel * 360).ToString(Inv) });
            }

            var paragraph = new Paragraph(pPr);
            foreach (var token in tokeny)
            {
                if (token.IsLineBreak)
                {
                    paragraph.Append(new Run(new Break()));
                    continue;
                }

                if (string.IsNullOrEmpty(token.Text)) continue;

                paragraph.Append(new Run(
                    OpenXmlWordElements.CreateRunProperties(Telo, token.Bold, token.Italic, strike: false, token.Underline),
                    new Text(token.Text) { Space = SpaceProcessingModeValues.Preserve }));
            }

            body.Append(paragraph);
        }
    }

    /// <summary>A4, okraje 2,5 cm, záhlaví „Příloha" vpravo, zápatí s číslem stránky na střed (spec B1, B2).</summary>
    private static SectionProperties Stranka(MainDocumentPart mainPart)
    {
        var zahlavi = mainPart.AddNewPart<HeaderPart>();
        zahlavi.Header = new Header(Odstavec("Příloha č.1 k Čj. MO ", zarovnani: JustificationValues.Right, po: 0));
        zahlavi.Header.Save();

        var zapati = mainPart.AddNewPart<FooterPart>();
        zapati.Footer = new Footer(new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            new SimpleField(new Run(
                OpenXmlWordElements.CreateRunProperties(Male, bold: false, italic: false, strike: false, underline: false),
                new Text("1"))) { Instruction = " PAGE " }));
        zapati.Footer.Save();

        return new SectionProperties(
            new HeaderReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(zahlavi) },
            new FooterReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(zapati) },
            new PageSize { Width = 11906U, Height = 16838U },
            new PageMargin
            {
                Top = 1417, Right = 1417U, Bottom = 1417, Left = 1417U,
                Header = 708U, Footer = 708U, Gutter = 0U,
            });
    }

    /// <summary>
    /// Část numbering: nadpisy sekcí arabsky, dvě řady nadpisů požadavků římsky (sekce 1 a 2)
    /// a seznamy z textu požadavku — každý seznam vlastní instance, aby začínal od 1.
    /// </summary>
    private sealed class Cislovani
    {
        private const int AbsSekce = 1;
        private const int AbsRimsky = 2;
        private const int AbsOdrazky = 3;
        private const int AbsCislovany = 4;

        private readonly List<NumberingInstance> _seznamy = new();
        private int _dalsiId = 100;

        public int NovySeznam(bool cislovany)
        {
            var id = _dalsiId++;
            _seznamy.Add(Instance(id, cislovany ? AbsCislovany : AbsOdrazky));
            return id;
        }

        public void Zapsat(MainDocumentPart mainPart)
        {
            // Schéma: všechny abstractNum před všemi num.
            var numbering = new Numbering(
                Jednourovnovy(AbsSekce, NumberFormatValues.Decimal),
                Jednourovnovy(AbsRimsky, NumberFormatValues.UpperRoman),
                Vicerovnovy(AbsOdrazky, cislovany: false),
                Vicerovnovy(AbsCislovany, cislovany: true),
                Instance(NumSekce, AbsSekce),
                Instance(NumPozadavkySekce1, AbsRimsky),
                Instance(NumPozadavkySekce2, AbsRimsky));
            numbering.Append(_seznamy);

            var part = mainPart.AddNewPart<NumberingDefinitionsPart>();
            part.Numbering = numbering;
            part.Numbering.Save();
        }

        private static AbstractNum Jednourovnovy(int id, NumberFormatValues format)
            => new(
                new MultiLevelType { Val = MultiLevelValues.SingleLevel },
                new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = format },
                    new LevelText { Val = "%1." },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new PreviousParagraphProperties(new Indentation { Left = "360", Hanging = "360" }))
                { LevelIndex = 0 })
            { AbstractNumberId = id };

        private static AbstractNum Vicerovnovy(int id, bool cislovany)
        {
            var abs = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel })
            {
                AbstractNumberId = id,
            };

            for (var uroven = 0; uroven < 3; uroven++)
            {
                abs.Append(new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = cislovany ? NumberFormatValues.Decimal : NumberFormatValues.Bullet },
                    new LevelText { Val = cislovany ? $"%{uroven + 1}." : "•" },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new PreviousParagraphProperties(new Indentation
                    {
                        Left = (720 + uroven * 360).ToString(Inv),
                        Hanging = "360",
                    }))
                { LevelIndex = uroven });
            }

            return abs;
        }

        /// <summary>
        /// StartOverride = 1: instance téže abstraktní definice by ve Wordu pokračovaly
        /// v číslování — sekce 2 by začala na XIX. a každý číslovaný seznam v textu požadavku
        /// by navazoval na předchozí.
        /// </summary>
        private static NumberingInstance Instance(int numId, int abstractId)
            => new(
                new AbstractNumId { Val = abstractId },
                new LevelOverride(new StartOverrideNumberingValue { Val = 1 }) { LevelIndex = 0 })
            { NumberID = numId };
    }
}
