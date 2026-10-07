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
/// Staví .docx výzvy tak, aby vypadal jako vzor <c>2027xxxx_N_8201_Vyzva_c_x_2027_EIS.docx</c>
/// (uživatel 2026-10-07: co je ve vzoru vidět, musí být ve vygenerovaném Wordu stejné). Formát
/// nese <see cref="VyzvaWordVzor"/> — styly, číslování, hlavička, tabulky, pevný text i prázdné
/// řádky převzaté z XML vzoru; tady se jen skládá pořadí a doplňují data. Od vzoru se záměrně liší
/// opravy z spec 2026-09-10 B9 a o řádek větší mezera před bodem 1 (uživatel 2026-10-06).
/// PDF výzvy je převod tohoto dokumentu (<see cref="WordNaHtml"/>), nemá vlastní šablonu. Údaje,
/// které aplikace nemá (č. j., datum, termín), zůstávají prázdné.
/// </summary>
public sealed class OpenXmlVyzvaExportService : IVyzvaWordExportService
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const int Telo = 24;               // 12 b.

    // Text požadavku a seznamy v něm — odsazení podle vzoru (twipy).
    private const int OdsazeniTextu = 426;
    private const int OdsazeniUrovne = 360;    // úroveň odsazení z editoru (ql-indent)
    private const int OdsazeniVnoreni = 720;   // vnořený seznam
    private const int OdrazkaVlevo = 1134;
    private const int OdrazkaPredsazeni = 425;
    private const int CisloVlevo = 426;
    private const int CisloPredsazeni = 357;

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

            var styly = mainPart.AddNewPart<StyleDefinitionsPart>();
            styly.Styles = VyzvaWordVzor.Styly();
            styly.Styles.Save();
            var nastaveni = mainPart.AddNewPart<DocumentSettingsPart>();
            nastaveni.Settings = VyzvaWordVzor.Nastaveni();
            nastaveni.Settings.Save();
            using (var motiv = VyzvaWordVzor.Motiv())
            {
                mainPart.AddNewPart<ThemePart>().FeedData(motiv);
            }

            body.Append(VyzvaWordVzor.HlavickaUradu(model.KodVyzvy, model.InformacniSystem));
            body.Append(VyzvaWordVzor.Uvod(model.CisloRamcoveSmlouvy, $"{model.PoradoveVRoce.ToString("00", Cs)}/{model.Rok}"));
            AppendPredmet(body, model, cislovani);
            AppendClovekohodiny(body, model);
            AppendCelkovaCena(body, model);
            body.Append(VyzvaWordVzor.Zaver(model.MistoPlneni));

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

    private static RunProperties Format(bool tucne = false, bool kurziva = false, bool podtrzeni = false)
        => OpenXmlWordElements.CreateRunProperties(Telo, tucne, kurziva, strike: false, podtrzeni);

    private static string CisloUkolu(VyzvaExportPozadavekViewModel p)
        => string.IsNullOrWhiteSpace(p.CisloUkoluVp)
            ? "Číslo úkolu VP EIS:"
            : $"Číslo úkolu VP EIS: {p.CisloUkoluVp}.";

    private static void AppendPredmet(Body body, VyzvaExportViewModel model, Cislovani cislovani)
    {
        body.Append(VyzvaWordVzor.NadpisSekce("Popis předmětu dílčí VZ na základě rámcové dohody, příp. počet "
                                              + "dodávaných souvisejících rozšíření licence APV a DZ"));
        body.Append(VyzvaWordVzor.PrazdnyPredTabulkouPredmetu());
        body.Append(VyzvaWordVzor.TabulkaPredmetu(model.Pozadavky.Select(p =>
            (p.PoradoveOznaceni, p.CisloUkoluVp ?? string.Empty, p.Nazev ?? string.Empty, p.CisloHtl))));
        body.Append(VyzvaWordVzor.PodTabulkouPredmetu());

        for (var i = 0; i < model.Pozadavky.Count; i++)
        {
            var p = model.Pozadavky[i];
            if (i > 0)
            {
                body.Append(VyzvaWordVzor.PrazdnyMeziPozadavkySekce1());
            }

            body.Append(VyzvaWordVzor.NadpisPozadavku(p.Nazev, sekce1: true));
            body.Append(VyzvaWordVzor.CisloUkolu(CisloUkolu(p), sekce1: true));
            PozadavekOdstavce(body, p.PozadavekHtml, cislovani);
            // Vždy „Bližší podrobnosti…", nikdy „Vazba na PMP" (rozhodnutí uživatele 2026-09-10).
            body.Append(VyzvaWordVzor.BlizsiPodrobnosti(p.CisloHtl));
        }
    }

    private static void AppendClovekohodiny(Body body, VyzvaExportViewModel model)
    {
        body.Append(VyzvaWordVzor.NadpisSekce("Počet člověkohodin s rozčleněním dle sazeb a informačních systémů", novaStrana: true));
        body.Append(VyzvaWordVzor.PrazdnyPodNadpisemSekce2());

        for (var i = 0; i < model.Pozadavky.Count; i++)
        {
            var p = model.Pozadavky[i];
            var k = p.Kalkulace;
            if (i > 0)
            {
                // Ve vzoru dva prázdné řádky mezi požadavky — za posledním žádný (sekce 3 je na nové stránce).
                body.Append(VyzvaWordVzor.PrazdnyRadekSekce2());
                body.Append(VyzvaWordVzor.PrazdnyRadekSekce2());
            }

            body.Append(VyzvaWordVzor.NadpisPozadavku(p.Nazev, sekce1: false));
            body.Append(VyzvaWordVzor.CisloUkolu(CisloUkolu(p), sekce1: false));

            // Nejdřív činnosti, pak licence; bez akceptované kalkulace žádná tabulka (spec B5).
            if (k.MaCinnosti)
            {
                body.Append(VyzvaWordVzor.StitekKalkulace("Individuální úpravy"));
                body.Append(VyzvaWordVzor.TabulkaCinnosti(
                    k.Radky.Select(r => new VyzvaWordVzor.RadekCinnosti(r.Kod, r.Nazev, Hodiny(r.Rozsah),
                        CastkaNeboPrazdno(r.Sazba), CastkaNeboPrazdno(r.CenaBezDph), CastkaNeboPrazdno(r.CenaDph),
                        CastkaNeboPrazdno(r.CenaSDph))),
                    new VyzvaWordVzor.Soucty(Castka(k.CelkemBezDph), Castka(k.CelkemDph), Castka(k.CelkemSDph))));
            }

            if (k.MaLicenci)
            {
                if (k.MaCinnosti)
                {
                    // Vzor tuto dvojici nemá: prázdný řádek odděluje licence od tabulky činností
                    // (uživatel 2026-10-06 chtěl před „Licenční rozšíření“ víc místa).
                    body.Append(VyzvaWordVzor.PrazdnyRadekSekce2());
                }

                body.Append(VyzvaWordVzor.StitekKalkulace("Licenční rozšíření"));
                body.Append(VyzvaWordVzor.TabulkaLicenci(
                    k.LicenceRadky.Select(r => new VyzvaWordVzor.RadekLicence(r.Kod.ToString(Inv), r.Nazev,
                        Castka(r.CenaBezDph), Castka(r.CenaDph), Castka(r.CenaSDph))),
                    new VyzvaWordVzor.Soucty(Castka(k.CenaLicence), Castka(k.LicenceRadky.Sum(r => r.CenaDph)),
                        Castka(k.LicenceRadky.Sum(r => r.CenaSDph)))));
            }
        }
    }

    private static void AppendCelkovaCena(Body body, VyzvaExportViewModel model)
    {
        body.Append(VyzvaWordVzor.NadpisSekce("Celková cena za člověkohodiny, příp. související rozšíření licence "
                                              + "APV a DZ IS GINIS® DEFENCE", novaStrana: true));
        var dph = $"DPH {DphProcenta} %";

        // Rekapitulace: pořadí ze sekce 1 (nepřečíslovává se), prázdný řádek, součty (spec B6).
        body.Append(VyzvaWordVzor.NadRekapitulaciUprav());
        body.Append(VyzvaWordVzor.Rekapitulace(false,
            model.Pozadavky.Where(p => p.Kalkulace.MaCinnosti).Select(p => new VyzvaWordVzor.RadekRekapitulace(
                p.PoradoveOznaceni, p.Nazev ?? string.Empty, Castka(p.Kalkulace.CelkemBezDph))),
            new VyzvaWordVzor.Soucty(Castka(model.CelkemBezDph), Castka(model.CelkemDph), Castka(model.CelkemSDph)), dph));

        body.Append(VyzvaWordVzor.NadRekapitulaciLicenci());
        body.Append(VyzvaWordVzor.Rekapitulace(true,
            model.Pozadavky.Where(p => p.Kalkulace.MaLicenci).Select(p => new VyzvaWordVzor.RadekRekapitulace(
                p.PoradoveOznaceni, p.Nazev ?? string.Empty, Castka(p.Kalkulace.CenaLicence))),
            new VyzvaWordVzor.Soucty(Castka(model.LicenceBezDph), Castka(model.LicenceDph), Castka(model.LicenceSDph)), dph));

        body.Append(VyzvaWordVzor.PrazdnyPredSouhrnem());
        body.Append(VyzvaWordVzor.Souhrn(
            new VyzvaWordVzor.Soucty(Castka(model.CelkemBezDph), Castka(model.CelkemDph), Castka(model.CelkemSDph)),
            new VyzvaWordVzor.Soucty(Castka(model.LicenceBezDph), Castka(model.LicenceDph), Castka(model.LicenceSDph)),
            new VyzvaWordVzor.Soucty(
                Castka(model.CelkemBezDph + model.LicenceBezDph),
                Castka(model.CelkemDph + model.LicenceDph),
                Castka(model.CelkemSDph + model.LicenceSDph)),
            DphProcenta));
    }

    /// <summary>
    /// Text požadavku jako ve vzoru: odstavce odsazené a zarovnané do bloku, mezi nimi prázdný řádek;
    /// odstavec hned před seznamem a položky téhož seznamu bez něj. Prázdné odstavce z editoru se
    /// vynechávají — mezery dává toto pravidlo, jinak by se zdvojily. Seznamy jsou skutečné seznamy
    /// Wordu: parser u položky nese druh a identitu seznamu a první token je značka, kterou Word
    /// kreslí sám (spec B8). Odkazy jako prostý text.
    /// </summary>
    private static void PozadavekOdstavce(Body body, string? safeHtml, Cislovani cislovani)
    {
        if (string.IsNullOrWhiteSpace(safeHtml))
        {
            return;
        }

        var bloky = RichTextHtmlParser.Parse(safeHtml).Where(o => !JePrazdny(o)).ToList();
        var seznamy = new Dictionary<int, int>(); // ListId parseru → numId Wordu

        for (var i = 0; i < bloky.Count; i++)
        {
            var odstavec = bloky[i];
            body.Append(odstavec.ListKind == RichTextListKind.None
                ? TextovyOdstavec(odstavec)
                : PolozkaSeznamu(odstavec, seznamy, cislovani));

            var bezMezery = i + 1 < bloky.Count
                && bloky[i + 1].ListKind != RichTextListKind.None
                && (odstavec.ListKind == RichTextListKind.None || odstavec.ListId == bloky[i + 1].ListId);
            if (!bezMezery)
            {
                body.Append(VyzvaWordVzor.PrazdnyRadekTextuPozadavku());
            }
        }
    }

    private static bool JePrazdny(RichTextParagraph odstavec)
        => odstavec.ListKind == RichTextListKind.None
           && odstavec.Tokens.All(t => t.IsLineBreak || string.IsNullOrWhiteSpace(t.Text));

    private static Paragraph TextovyOdstavec(RichTextParagraph odstavec)
    {
        var paragraph = new Paragraph(new ParagraphProperties(
            new Indentation { Left = (OdsazeniTextu + odstavec.IndentLevel * OdsazeniUrovne).ToString(Inv) },
            new Justification { Val = JustificationValues.Both }));
        AppendTokeny(paragraph, odstavec.Tokens);
        return paragraph;
    }

    /// <summary>Položka seznamu ve stylu Odstavec se seznamem vzoru, písmo přebité na Times New Roman 12 b.</summary>
    private static Paragraph PolozkaSeznamu(RichTextParagraph odstavec, Dictionary<int, int> seznamy, Cislovani cislovani)
    {
        var cislovany = odstavec.ListKind == RichTextListKind.Ordered;
        if (!seznamy.TryGetValue(odstavec.ListId, out var numId))
        {
            numId = cislovani.NovySeznam(cislovany);
            seznamy[odstavec.ListId] = numId;
        }

        var uroven = Math.Min(odstavec.IndentLevel, 2);
        var (vlevo, predsazeni) = cislovany ? (CisloVlevo, CisloPredsazeni) : (OdrazkaVlevo, OdrazkaPredsazeni);
        var paragraph = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "Odstavecseseznamem" },
            new NumberingProperties(
                new NumberingLevelReference { Val = uroven },
                new NumberingId { Val = numId }),
            new SpacingBetweenLines { After = "0" },
            new Indentation
            {
                Left = (vlevo + uroven * OdsazeniVnoreni).ToString(Inv),
                Hanging = predsazeni.ToString(Inv),
            },
            new Justification { Val = JustificationValues.Both },
            new ParagraphMarkRunProperties(Format().ChildElements.Select(e => e.CloneNode(true)))));
        AppendTokeny(paragraph, odstavec.Tokens.Skip(1));
        return paragraph;
    }

    private static void AppendTokeny(Paragraph paragraph, IEnumerable<RichTextToken> tokeny)
    {
        foreach (var token in tokeny)
        {
            if (token.IsLineBreak)
            {
                paragraph.Append(new Run(Format(token.Bold, token.Italic, token.Underline), new Break()));
                continue;
            }

            if (string.IsNullOrEmpty(token.Text)) continue;

            paragraph.Append(new Run(
                Format(token.Bold, token.Italic, token.Underline),
                new Text(token.Text) { Space = SpaceProcessingModeValues.Preserve }));
        }
    }

    /// <summary>A4, okraje 2,5 cm, záhlaví a zápatí ze vzoru, sloupce a mřížka stránky vzoru (spec B1, B2).</summary>
    private static SectionProperties Stranka(MainDocumentPart mainPart)
    {
        var zahlavi = mainPart.AddNewPart<HeaderPart>();
        zahlavi.Header = VyzvaWordVzor.Zahlavi();
        zahlavi.Header.Save();

        var zapati = mainPart.AddNewPart<FooterPart>();
        zapati.Footer = VyzvaWordVzor.Zapati();
        zapati.Footer.Save();

        return new SectionProperties(
            new HeaderReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(zahlavi) },
            new FooterReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(zapati) },
            new PageSize { Width = 11906U, Height = 16838U },
            new PageMargin
            {
                Top = 1417, Right = 1417U, Bottom = 1417, Left = 1417U,
                Header = 708U, Footer = 708U, Gutter = 0U,
            },
            new Columns { Space = "708" },
            new DocGrid { LinePitch = 360 });
    }

    /// <summary>
    /// Část numbering: definice vzoru (sekce, dvě římské řady požadavků, odrážky, číslovaný seznam)
    /// a pro každý seznam z textu požadavku vlastní instance, aby číslovaný seznam začínal od 1.
    /// </summary>
    private sealed class Cislovani
    {
        private readonly List<NumberingInstance> _seznamy = new();
        private int _dalsiId = 100;

        public int NovySeznam(bool cislovany)
        {
            var id = _dalsiId++;
            // StartOverride = 1: instance téže abstraktní definice by ve Wordu pokračovaly v číslování.
            _seznamy.Add(new NumberingInstance(
                new AbstractNumId { Val = cislovany ? VyzvaWordVzor.AbsCislovany : VyzvaWordVzor.AbsOdrazky },
                new LevelOverride(new StartOverrideNumberingValue { Val = 1 }) { LevelIndex = 0 })
            {
                NumberID = id,
            });
            return id;
        }

        public void Zapsat(MainDocumentPart mainPart)
        {
            var numbering = VyzvaWordVzor.Cislovani();
            numbering.Append(_seznamy);

            var part = mainPart.AddNewPart<NumberingDefinitionsPart>();
            part.Numbering = numbering;
            part.Numbering.Save();
        }
    }
}
