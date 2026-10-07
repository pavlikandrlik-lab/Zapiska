using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PmTracker.Web.Services.Export;

/// <summary>
/// HTML dokumentu Wordu pro sazbu prohlížečem: celý dokument včetně stylu stránky a šablony
/// záhlaví a zápatí (prohlížeč je vysází na každou stránku).
/// </summary>
public sealed record WordHtmlDokument(string Html, string ZahlaviSablona, string ZapatiSablona);

/// <summary>
/// Převádí Word výzvy na HTML, ze kterého prohlížeč vysází PDF (uživatel 2026-10-07: „dej do stejného
/// formátu i PDF“). PDF tak nemá vlastní šablonu, která by se mohla od Wordu, a tím od vzoru, rozejít:
/// obsah, pořadí, prázdné řádky i formát (písmo, velikost, tučnost, odsazení, mezery, číslování,
/// tabulky se šířkami, čarami a podbarvením, záhlaví a zápatí) se čtou z dokumentu.
///
/// Převod umí právě tu část WordprocessingML, kterou dokument výzvy používá. Co nezná, ohlásí
/// <see cref="NeznameVlastnosti"/>; test hlídá, že je to prázdné. Pravidla sazby změřená na Wordu
/// (2026-10-07, výzva z dat vzoru vykreslená Wordem do PDF):
/// - Výška řádku = (win ascent + descent + vnější proklad písma) × velikost × násobek řádkování.
///   Prohlížeč by počítal z jiných metrik, proto se řádkování píše v bodech. Běhy jen z mezer
///   a tabulátorů řádek nezvyšují, značka seznamu ano (odrážka písmem Symbol).
/// - Výchozí zarážky tabulátoru jsou násobky defaultTabStop od levého okraje; sází je prohlížeč
///   (tab-size). Vlastní zarážka ruší výchozí zarážky před sebou. Text za tabulátorem na vlastní
///   zarážce se umístí na zarážku — předpoklad: text před ním se do ní vejde.
/// - V odstavci do bloku Word smrští mezery až o čtvrtinu, vejde-li se tím do řádku další slovo
///   (ponechal 24,8 %, zalomil od 26,4 %); prohlížeči se mezery o čtvrtinu zúží (word-spacing).
///   Výjimka, kterou CSS nenapodobí: když by bez slova stačilo řádek roztáhnout jen málo, Word
///   raději roztahuje (2 ze 115 změřených řádků). Poslední řádek odstavce má tak mezery o čtvrtinu užší.
/// - Word hlídá osamocené řádky, i když to dokument neuvádí (orphans/widows 2); odstavec o třech
///   řádcích nedělí — to prohlížeči doplní skript po sazbě.
/// - Boční okraje stránky jsou odsazení těla s pevnou šířkou sloupce: obsah v okraji stránky by
///   prohlížeč ořízl (Word do něj kreslí čísla zarovnaná vpravo) a rozměr stránky zaokrouhluje.
/// - Mezera před odstavcem je padding, za odstavcem margin: ve Wordu se sčítají (dva okraje by se
///   slily) a mezera za odstavcem na konci stránky přetéká do okraje (okraj prohlížeč na zlomu zahodí).
/// - Tabulka začíná levou čarou na okraji (režim kompatibility 15), čáry leží na hranách mřížky.
///   Text buňky má šířku sloupce bez okrajů buňky a výška řádku je bez čar (řádek 14,4 b. s čarou
///   0,5 b. má rozteč 14,9 b.); prohlížeč čáru počítá napůl do buňky, proto se o ni vyrovná.
/// - Číslo seznamu stojí na začátku prvního řádku (levý okraj − předsazení), text na levém okraji;
///   číslo zarovnané vpravo končí na začátku prvního řádku. Odrážky symbolových písem se kreslí
///   jako tvar s obrysem znaku z písma Windows — prohlížeč je spolehlivě nevykreslí.
/// </summary>
public static class WordNaHtml
{
    public static WordHtmlDokument Preved(byte[] docx, string titulek)
    {
        using var stream = new MemoryStream(docx, writable: false);
        using var dokument = WordprocessingDocument.Open(stream, false);
        return new Prevod(dokument).Dokument(titulek);
    }

    /// <summary>Prvky a hodnoty dokumentu, které převod nezná — v PDF by se neprojevily.</summary>
    public static IReadOnlyCollection<string> NeznameVlastnosti(byte[] docx)
    {
        using var stream = new MemoryStream(docx, writable: false);
        using var dokument = WordprocessingDocument.Open(stream, false);
        var prevod = new Prevod(dokument);
        prevod.Dokument(string.Empty);
        prevod.Zkontroluj();
        return prevod.Nezname;
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string Pt(double hodnota) => Math.Round(hodnota, 3).ToString("0.###", Inv) + "pt";

    private static double Tw(string? twipy) => string.IsNullOrEmpty(twipy) ? 0 : int.Parse(twipy, Inv) / 20d;

    private static string Esc(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static bool Zapnuto(OnOffType prvek) => prvek.Val is null || prvek.Val.Value;

    /// <summary>
    /// Písma dokumentu: CSS, výška jednoduchého řádku — (usWinAscent + usWinDescent + vnější proklad)
    /// / jednotky em podle písem Windows, jak ji počítá Word — a šířka mezery v em.
    /// </summary>
    private static readonly Dictionary<string, (string Css, double Radek, double Mezera)> Pisma = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Times New Roman"] = ("'Times New Roman',Times,serif", 2355d / 2048, 512d / 2048),
        ["Calibri"] = ("Calibri,Carlito,sans-serif", 2500d / 2048, 463d / 2048),
        ["Segoe UI"] = ("'Segoe UI',sans-serif", 2724d / 2048, 562d / 2048),
        ["Courier New"] = ("'Courier New',Courier,monospace", 2320d / 2048, 1229d / 2048),
        ["Symbol"] = ("Symbol", 2509d / 2048, 512d / 2048),
        ["Wingdings"] = ("Wingdings", 2273d / 2048, 2048d / 2048),
    };

    /// <summary>Obrys znaku písma v em: odsazení zleva, výška spodku nad účařím, rozměr, posun na další znak.</summary>
    private readonly record struct Tvar(double Vlevo, double Dole, double Rozmer, double Posun, bool Kruh);

    /// <summary>
    /// Odrážky symbolových písem (soukromá oblast Unicode) — prohlížeč je neumí spolehlivě vykreslit,
    /// kreslí se jako tvar s obrysem znaku z písem Windows (jednotky z 2048 na em). Text značky zůstává
    /// běžným znakem kvůli kopírování a hledání.
    /// </summary>
    private static readonly Dictionary<(string Pismo, char Znak), (char Text, Tvar Tvar)> Symboly = new()
    {
        [("Symbol", '\uF0B7')] = ('\u2022', new Tvar(0, 211d / 2048, 731d / 2048, 942d / 2048, Kruh: true)),
        [("Wingdings", '\uF0A7')] = ('\u25AA', new Tvar(173d / 2048, 444d / 2048, 592d / 2048, 937d / 2048, Kruh: false)),
    };

    // Známé potomky prvků (podle místního jména rodiče). Ostatní prvky jsou listy bez potomků.
    private static readonly Dictionary<string, HashSet<string>> Znami = new()
    {
        ["body"] = new() { "p", "tbl", "sectPr", "sdt", "bookmarkStart", "bookmarkEnd" },
        ["hdr"] = new() { "p", "tbl", "sdt" },
        ["ftr"] = new() { "p", "tbl", "sdt" },
        ["p"] = new() { "pPr", "r", "sdt", "fldSimple", "hyperlink", "bookmarkStart", "bookmarkEnd", "proofErr" },
        // Nevizuální nebo bez účinku v dokumentu: outlineLvl, overflowPunct, autoSpaceDE/DN, adjustRightInd,
        // suppressAutoHyphens (automatické dělení slov je vypnuté), textAlignment (účaří je výchozí).
        ["pPr"] = new()
        {
            "pStyle", "keepNext", "pageBreakBefore", "widowControl", "numPr", "tabs", "jc", "ind", "spacing", "pBdr", "rPr",
            "outlineLvl", "overflowPunct", "autoSpaceDE", "autoSpaceDN", "adjustRightInd", "suppressAutoHyphens", "textAlignment",
        },
        ["numPr"] = new() { "ilvl", "numId" },
        ["tabs"] = new() { "tab" },
        ["pBdr"] = new() { "bottom" },
        ["r"] = new() { "rPr", "t", "tab", "br", "fldChar", "instrText", "lastRenderedPageBreak" },
        // bCs, iCs, szCs: písmo složitých písem; lang: jazyk kontroly pravopisu.
        ["rPr"] = new() { "rStyle", "rFonts", "b", "bCs", "i", "iCs", "u", "sz", "szCs", "color", "spacing", "lang" },
        ["sdt"] = new() { "sdtPr", "sdtEndPr", "sdtContent" },
        ["sdtContent"] = new() { "p", "r", "tbl" },
        ["hyperlink"] = new() { "r" },
        ["fldSimple"] = new() { "r" },
        ["tbl"] = new() { "tblPr", "tblGrid", "tr" },
        // tblLook: podmíněný formát stylu tabulky; dokument styl tabulky s ním nemá.
        ["tblPr"] = new() { "tblW", "tblInd", "tblLayout", "tblBorders", "tblCellMar", "tblLook" },
        ["tblBorders"] = new() { "top", "left", "start", "bottom", "right", "end", "insideH", "insideV" },
        ["tblCellMar"] = new() { "top", "left", "start", "bottom", "right", "end" },
        ["tblGrid"] = new() { "gridCol" },
        ["tr"] = new() { "trPr", "tc" },
        ["trPr"] = new() { "cantSplit", "trHeight" },
        ["tc"] = new() { "tcPr", "p", "tbl" },
        // tcW: šířky dává mřížka; noWrap u šířky v twipech Word nepoužije; hideMark: prázdná buňka je nižší než řádek.
        ["tcPr"] = new() { "tcW", "gridSpan", "tcBorders", "shd", "vAlign", "noWrap", "hideMark" },
        ["tcBorders"] = new() { "top", "left", "start", "bottom", "right", "end" },
        // cols: jeden sloupec; docGrid bez typu mřížky na řádky nemá vliv.
        ["sectPr"] = new() { "headerReference", "footerReference", "pgSz", "pgMar", "cols", "docGrid" },
        ["lvl"] = new() { "start", "numFmt", "lvlText", "lvlJc", "pPr", "rPr" },
        ["style"] = new() { "name", "aliases", "basedOn", "next", "link", "uiPriority", "semiHidden", "unhideWhenUsed", "qFormat", "locked", "pPr", "rPr", "tblPr" },
    };

    private readonly record struct Zarazka(double Pozice, string Druh);

    private sealed class OdstavecV
    {
        public string Zarovnani = "left";
        public double Vlevo, Vpravo, PrvniRadek;   // PrvniRadek < 0 = předsazení
        public double Pred, Za;
        public bool PredAuto, ZaAuto, DrzetSDalsim, NovaStrana;
        public bool Vdovy = true;                   // Word hlídá osamocené řádky, i když to dokument neuvádí
        public int Radkovani = 240;
        public string Pravidlo = "auto";
        public string? SpodniCara;
        public double SpodniCaraMezera;
        public List<Zarazka> Zarazky = new();
        public int? NumId;
        public int Uroven;
    }

    private sealed class BehV
    {
        public string Pismo = "Times New Roman";
        public double Velikost = 10;               // Word bez w:sz = 10 b.
        public bool Tucne, Kurziva, Podtrzeni;
        public string Barva = "#000";
        public double Prolozeni;

        public BehV Kopie() => (BehV)MemberwiseClone();
    }

    private sealed record Uroven(string Format, string Text, string Zarovnani, int Start, OpenXmlElement? Odstavec, OpenXmlElement? Beh);

    private sealed record Instance(int Abstraktni, IReadOnlyDictionary<int, int> Starty);

    private readonly record struct Stranka(double Sirka, double Vyska, double Nahore, double Vpravo, double Dole, double Vlevo, double Zahlavi, double Zapati)
    {
        /// <summary>Šířka sloupce textu — pevně, prohlížeč rozměr stránky zaokrouhluje.</summary>
        public double Sloupec => Sirka - Vlevo - Vpravo;
    }

    private sealed class Prevod
    {
        private readonly MainDocumentPart _hlavni;
        private readonly Dictionary<string, Style> _styly = new();
        private readonly string? _vychoziOdstavcovy;
        private readonly string? _vychoziZnakovy;
        private readonly OpenXmlElement? _vychoziTabulka;
        private readonly OpenXmlElement? _vychoziBeh;
        private readonly OpenXmlElement? _vychoziOdstavec;
        private readonly string _pismoMotivu;
        private readonly string _pismoNadpisuMotivu;
        private readonly double _tabulator;
        private readonly string _jazyk;
        private readonly Dictionary<int, Dictionary<int, Uroven>> _abstraktni = new();
        private readonly Dictionary<int, Instance> _instance = new();
        private readonly Dictionary<string, int[]> _citace = new();

        public SortedSet<string> Nezname { get; } = new(StringComparer.Ordinal);

        public Prevod(WordprocessingDocument dokument)
        {
            _hlavni = dokument.MainDocumentPart ?? throw new InvalidOperationException("Dokument nemá hlavní část.");

            var styly = _hlavni.StyleDefinitionsPart?.Styles;
            foreach (var styl in styly?.Elements<Style>() ?? Enumerable.Empty<Style>())
            {
                if (styl.StyleId?.Value is not { } id) continue;
                _styly[id] = styl;
                if (styl.Default?.Value != true) continue;
                if (styl.Type?.Value == StyleValues.Paragraph) _vychoziOdstavcovy = id;
                else if (styl.Type?.Value == StyleValues.Character) _vychoziZnakovy = id;
                else if (styl.Type?.Value == StyleValues.Table) _vychoziTabulka = styl.StyleTableProperties;
            }

            var vychozi = styly?.DocDefaults;
            _vychoziBeh = vychozi?.RunPropertiesDefault?.RunPropertiesBaseStyle;
            _vychoziOdstavec = vychozi?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle;
            _jazyk = _vychoziBeh?.GetFirstChild<Languages>()?.Val?.Value ?? "cs-CZ";

            var pisma = _hlavni.ThemePart?.Theme?.ThemeElements?.FontScheme;
            _pismoMotivu = pisma?.MinorFont?.LatinFont?.Typeface?.Value ?? "Calibri";
            _pismoNadpisuMotivu = pisma?.MajorFont?.LatinFont?.Typeface?.Value ?? "Cambria";

            var tabulator = _hlavni.DocumentSettingsPart?.Settings?.GetFirstChild<DefaultTabStop>()?.Val?.Value;
            _tabulator = tabulator is { } t2 ? t2 / 20d : 36;

            var cislovani = _hlavni.NumberingDefinitionsPart?.Numbering;
            foreach (var abstraktni in cislovani?.Elements<AbstractNum>() ?? Enumerable.Empty<AbstractNum>())
            {
                var urovne = new Dictionary<int, Uroven>();
                foreach (var lvl in abstraktni.Elements<Level>())
                {
                    urovne[lvl.LevelIndex?.Value ?? 0] = new Uroven(
                        lvl.NumberingFormat?.Val?.InnerText ?? "decimal",
                        lvl.LevelText?.Val?.Value ?? string.Empty,
                        lvl.LevelJustification?.Val?.InnerText ?? "left",
                        lvl.StartNumberingValue?.Val?.Value ?? 0,
                        lvl.PreviousParagraphProperties,
                        lvl.NumberingSymbolRunProperties);
                }

                _abstraktni[abstraktni.AbstractNumberId?.Value ?? 0] = urovne;
            }

            foreach (var instance in cislovani?.Elements<NumberingInstance>() ?? Enumerable.Empty<NumberingInstance>())
            {
                var starty = instance.Elements<LevelOverride>()
                    .Where(o => o.StartOverrideNumberingValue?.Val is not null)
                    .ToDictionary(o => o.LevelIndex?.Value ?? 0, o => o.StartOverrideNumberingValue!.Val!.Value);
                _instance[instance.NumberID?.Value ?? 0] = new Instance(instance.AbstractNumId?.Val?.Value ?? 0, starty);
            }
        }

        public WordHtmlDokument Dokument(string titulek)
        {
            var telo = _hlavni.Document?.Body ?? throw new InvalidOperationException("Dokument nemá tělo.");
            var oddil = telo.Elements<SectionProperties>().LastOrDefault() ?? new SectionProperties();
            var stranka = Stranka(oddil);

            var obsah = new StringBuilder();
            Bloky(telo.ChildElements, obsah);

            // Boční okraje jako odsazení těla: obsah v okraji stránky by prohlížeč ořízl, Word do něj kreslí
            // (čísla požadavků zarovnaná vpravo, XVIII.).
            var styl = new StringBuilder()
                .Append("@page{size:").Append(Pt(stranka.Sirka)).Append(' ').Append(Pt(stranka.Vyska))
                .Append(";margin:").Append(Pt(stranka.Nahore)).Append(" 0 ").Append(Pt(stranka.Dole)).Append(" 0}")
                .Append("body{margin:0;padding:0 ").Append(Pt(stranka.Vpravo)).Append(" 0 ").Append(Pt(stranka.Vlevo))
                .Append(";width:").Append(Pt(stranka.Sloupec)).Append(';').Append(ZakladTela).Append('}')
                .Append(ZakladniStyl)
                // Náhradní HTML tisk (když PDF nevznikne): stránka na střed obrazovky.
                .Append("@media screen{body{margin:24pt auto}}");

            var html = new StringBuilder()
                .Append("<!DOCTYPE html><html lang=\"").Append(Esc(_jazyk)).Append("\"><head><meta charset=\"utf-8\"/><title>")
                .Append(Esc(titulek)).Append("</title><style>").Append(styl).Append("</style></head><body>")
                .Append(obsah).Append("<script>").Append(TriRadky).Append("</script></body></html>");

            return new WordHtmlDokument(
                html.ToString(),
                Sablona(oddil, stranka, zahlavi: true),
                Sablona(oddil, stranka, zahlavi: false));
        }

        // Odstavec ve Wordu: mezery se nehroutí, žádné vlastní okraje; tabulka se slitými čarami a pevnou mřížkou.
        // Kerning a ligatury Word bez nastavení nepoužívá.
        /// <summary>
        /// Word s hlídáním osamocených řádků nerozdělí odstavec o třech řádcích (2+1 ani 1+2) a přesune ho
        /// celý; prohlížeč v tom případě widows obětuje a rozdělí 2+1. Počet řádků je známý až po sazbě.
        /// </summary>
        private const string TriRadky =
            "(function(){var p=document.getElementsByTagName('p');for(var i=0;p[i];i++){var s=getComputedStyle(p[i]);"
            + "if(s.widows==='1')continue;var v=p[i].clientHeight-parseFloat(s.paddingTop)-parseFloat(s.paddingBottom);"
            + "if(Math.round(v/parseFloat(s.lineHeight))===3)p[i].style.breakInside='avoid';}})();";

        private const string ZakladTela = "color:#000;background:#fff;font-kerning:none;font-variant-ligatures:none";

        private const string ZakladniStyl =
            "html{-webkit-print-color-adjust:exact;print-color-adjust:exact}"
            + "p{margin:0;white-space:pre-wrap;orphans:2;widows:2}"
            + "table{border-collapse:collapse;table-layout:fixed}"
            + "td{padding:0}";

        /// <summary>Kontrola, že dokument nepoužívá nic, co převod nezná (tělo, záhlaví, zápatí, styly, číslování).</summary>
        public void Zkontroluj()
        {
            Zkontroluj(_hlavni.Document?.Body);
            foreach (var cast in _hlavni.HeaderParts) Zkontroluj(cast.Header);
            foreach (var cast in _hlavni.FooterParts) Zkontroluj(cast.Footer);
            foreach (var styl in _styly.Values) Zkontroluj(styl);
            Zkontroluj(_vychoziBeh);
            Zkontroluj(_vychoziOdstavec);
            foreach (var lvl in _hlavni.NumberingDefinitionsPart?.Numbering?.Descendants<Level>() ?? Enumerable.Empty<Level>())
            {
                Zkontroluj(lvl);
            }
        }

        private void Zkontroluj(OpenXmlElement? prvek)
        {
            if (prvek is null || prvek.LocalName == "sdtPr") return;
            Znami.TryGetValue(prvek.LocalName, out var znami);
            foreach (var potomek in prvek.ChildElements)
            {
                if (znami?.Contains(potomek.LocalName) != true)
                {
                    Nezname.Add($"{prvek.LocalName}/{potomek.LocalName}");
                    continue;
                }

                Zkontroluj(potomek);
            }
        }

        // ---------- bloky ----------

        private void Bloky(IEnumerable<OpenXmlElement> prvky, StringBuilder html)
        {
            foreach (var prvek in prvky)
            {
                switch (prvek)
                {
                    case Paragraph odstavec: Odstavec(odstavec, html); break;
                    case Table tabulka: Tabulka(tabulka, html); break;
                    case SdtBlock sdt when sdt.SdtContentBlock is { } obsah: Bloky(obsah.ChildElements, html); break;
                }
            }
        }

        // ---------- styly ----------

        private List<Style> Retez(string? stylId)
        {
            var retez = new List<Style>();
            var navstivene = new HashSet<string>();
            while (stylId is not null && navstivene.Add(stylId) && _styly.TryGetValue(stylId, out var styl))
            {
                retez.Add(styl);
                stylId = styl.BasedOn?.Val?.Value;
            }

            retez.Reverse();
            return retez;
        }

        private BehV VychoziBeh()
        {
            var beh = new BehV();
            Prevezmi(beh, _vychoziBeh);
            return beh;
        }

        private string PismoMotivu(ThemeFontValues pismo)
            => pismo == ThemeFontValues.MajorAscii || pismo == ThemeFontValues.MajorHighAnsi
               || pismo == ThemeFontValues.MajorBidi || pismo == ThemeFontValues.MajorEastAsia
                ? _pismoNadpisuMotivu
                : _pismoMotivu;

        private void Prevezmi(BehV beh, OpenXmlElement? rPr)
        {
            if (rPr is null) return;
            foreach (var vlastnost in rPr.ChildElements)
            {
                switch (vlastnost)
                {
                    case RunFonts pismo:
                        if (pismo.AsciiTheme?.Value is { } motiv) beh.Pismo = PismoMotivu(motiv);
                        else if ((pismo.Ascii?.Value ?? pismo.HighAnsi?.Value) is { } nazev) beh.Pismo = nazev;
                        break;
                    case Bold tucne: beh.Tucne = Zapnuto(tucne); break;
                    case Italic kurziva: beh.Kurziva = Zapnuto(kurziva); break;
                    case Underline podtrzeni:
                        beh.Podtrzeni = podtrzeni.Val is null || podtrzeni.Val.Value != UnderlineValues.None;
                        if (podtrzeni.Val is not null && podtrzeni.Val.Value != UnderlineValues.Single && podtrzeni.Val.Value != UnderlineValues.None)
                        {
                            Nezname.Add($"u={podtrzeni.Val.InnerText}");
                        }
                        break;
                    case FontSize velikost when velikost.Val?.Value is { } pulBody:
                        beh.Velikost = int.Parse(pulBody, Inv) / 2d;
                        break;
                    case Color barva:
                        beh.Barva = barva.Val?.Value is { } hex && hex != "auto" ? "#" + hex : "#000";
                        break;
                    case Spacing prolozeni: beh.Prolozeni = (prolozeni.Val?.Value ?? 0) / 20d; break;
                }
            }
        }

        private void Prevezmi(OdstavecV odstavec, OpenXmlElement? pPr)
        {
            if (pPr is null) return;
            foreach (var vlastnost in pPr.ChildElements)
            {
                switch (vlastnost)
                {
                    case Justification zarovnani: odstavec.Zarovnani = Zarovnani(zarovnani.Val); break;
                    case Indentation odsazeni:
                        if ((odsazeni.Left?.Value ?? odsazeni.Start?.Value) is { } vlevo) odstavec.Vlevo = Tw(vlevo);
                        if ((odsazeni.Right?.Value ?? odsazeni.End?.Value) is { } vpravo) odstavec.Vpravo = Tw(vpravo);
                        if (odsazeni.Hanging?.Value is { } predsazeni) odstavec.PrvniRadek = -Tw(predsazeni);
                        else if (odsazeni.FirstLine?.Value is { } prvni) odstavec.PrvniRadek = Tw(prvni);
                        break;
                    case SpacingBetweenLines mezery:
                        if (mezery.Before?.Value is { } pred) odstavec.Pred = Tw(pred);
                        if (mezery.After?.Value is { } za) odstavec.Za = Tw(za);
                        if (mezery.BeforeAutoSpacing is { } predAuto) odstavec.PredAuto = predAuto.Value;
                        if (mezery.AfterAutoSpacing is { } zaAuto) odstavec.ZaAuto = zaAuto.Value;
                        if (mezery.Line?.Value is { } radek) odstavec.Radkovani = int.Parse(radek, Inv);
                        if (mezery.LineRule?.Value is { } pravidlo)
                        {
                            odstavec.Pravidlo = pravidlo == LineSpacingRuleValues.Exact ? "exact"
                                : pravidlo == LineSpacingRuleValues.AtLeast ? "atLeast" : "auto";
                        }
                        break;
                    case KeepNext drzet: odstavec.DrzetSDalsim = Zapnuto(drzet); break;
                    case PageBreakBefore zalomit: odstavec.NovaStrana = Zapnuto(zalomit); break;
                    case WidowControl vdovy: odstavec.Vdovy = Zapnuto(vdovy); break;
                    case NumberingProperties cislovani:
                        if (cislovani.NumberingId?.Val?.Value is { } numId) odstavec.NumId = numId;
                        odstavec.Uroven = cislovani.NumberingLevelReference?.Val?.Value ?? 0;
                        break;
                    case Tabs zarazky:
                        foreach (var zarazka in zarazky.Elements<TabStop>())
                        {
                            var pozice = (zarazka.Position?.Value ?? 0) / 20d;
                            odstavec.Zarazky.RemoveAll(z => Math.Abs(z.Pozice - pozice) < 0.01);
                            var druh = zarazka.Val?.Value;
                            if (druh == TabStopValues.Clear) continue;
                            if (druh != TabStopValues.Left && druh != TabStopValues.Center && druh != TabStopValues.Right)
                            {
                                Nezname.Add($"tab={zarazka.Val?.InnerText}");
                            }

                            odstavec.Zarazky.Add(new Zarazka(pozice,
                                druh == TabStopValues.Center ? "center" : druh == TabStopValues.Right ? "right" : "left"));
                        }
                        break;
                    case ParagraphBorders cary when cary.BottomBorder is { } spodni:
                        odstavec.SpodniCara = Cara(spodni);
                        odstavec.SpodniCaraMezera = spodni.Space?.Value ?? 0;
                        break;
                }
            }
        }

        private string Zarovnani(EnumValue<JustificationValues>? hodnota)
        {
            var zarovnani = hodnota?.Value;
            if (zarovnani is null || zarovnani == JustificationValues.Left || zarovnani == JustificationValues.Start) return "left";
            if (zarovnani == JustificationValues.Center) return "center";
            if (zarovnani == JustificationValues.Right || zarovnani == JustificationValues.End) return "right";
            if (zarovnani == JustificationValues.Both) return "justify";
            Nezname.Add($"jc={hodnota?.InnerText}");
            return "left";
        }

        private string Cara(BorderType? cara)
        {
            if (cara?.Val is null || cara.Val.Value == BorderValues.Nil || cara.Val.Value == BorderValues.None) return "none";
            if (cara.Val.Value != BorderValues.Single) Nezname.Add($"čára={cara.Val.InnerText}");
            var barva = cara.Color?.Value is { } hex && hex != "auto" ? "#" + hex : "#000";
            return $"{Pt((cara.Size?.Value ?? 4) / 8d)} solid {barva}";
        }

        private static (string Css, double Radek, double Mezera) Pismo(string nazev)
            => Pisma.TryGetValue(nazev, out var pismo) ? pismo : ($"'{nazev}',serif", 1.15, 0.25);

        private double VyskaRadku(BehV beh, OdstavecV odstavec)
        {
            var jednoduchy = Pismo(beh.Pismo).Radek * beh.Velikost;
            return odstavec.Pravidlo switch
            {
                "exact" => odstavec.Radkovani / 20d,
                "atLeast" => Math.Max(jednoduchy, odstavec.Radkovani / 20d),
                _ => jednoduchy * odstavec.Radkovani / 240d,
            };
        }

        private static void PismoCss(StringBuilder css, BehV beh)
        {
            css.Append("font-family:").Append(Pismo(beh.Pismo).Css).Append(";font-size:").Append(Pt(beh.Velikost)).Append(';');
            if (beh.Tucne) css.Append("font-weight:bold;");
            if (beh.Kurziva) css.Append("font-style:italic;");
            if (beh.Barva != "#000") css.Append("color:").Append(beh.Barva).Append(';');
            if (beh.Prolozeni != 0) css.Append("letter-spacing:").Append(Pt(beh.Prolozeni)).Append(';');
        }

        // ---------- odstavec ----------

        private void Odstavec(Paragraph p, StringBuilder html)
        {
            var pPr = p.ParagraphProperties;
            var retez = Retez(pPr?.ParagraphStyleId?.Val?.Value ?? _vychoziOdstavcovy);

            var odstavec = new OdstavecV();
            var znacka = VychoziBeh();
            Prevezmi(odstavec, _vychoziOdstavec);
            foreach (var styl in retez)
            {
                Prevezmi(odstavec, styl.StyleParagraphProperties);
                Prevezmi(znacka, styl.StyleRunProperties);
            }

            // Pořadí jako ve Wordu: styl, úroveň číslování, přímý formát.
            var numId = pPr?.NumberingProperties?.NumberingId?.Val?.Value ?? odstavec.NumId;
            var uroven = pPr?.NumberingProperties?.NumberingLevelReference?.Val?.Value ?? odstavec.Uroven;
            var lvl = numId is { } id && id != 0 && _instance.TryGetValue(id, out var instance)
                      && _abstraktni.TryGetValue(instance.Abstraktni, out var urovne) && urovne.TryGetValue(uroven, out var l)
                ? l
                : null;
            if (lvl is not null) Prevezmi(odstavec, lvl.Odstavec);
            Prevezmi(odstavec, pPr);
            Prevezmi(znacka, pPr?.ParagraphMarkRunProperties);

            var radek = VyskaRadku(znacka, odstavec);
            var vlastniZarazky = odstavec.Zarazky.Count > 0 && p.Descendants<TabChar>().Any();

            var css = new StringBuilder();
            PismoCss(css, znacka);
            css.Append("line-height:").Append(Pt(radek)).Append(';');
            if (odstavec.Vlevo != 0) css.Append("padding-left:").Append(Pt(odstavec.Vlevo)).Append(';');
            if (odstavec.Vpravo != 0) css.Append("padding-right:").Append(Pt(odstavec.Vpravo)).Append(';');
            if (odstavec.PrvniRadek != 0) css.Append("text-indent:").Append(Pt(odstavec.PrvniRadek)).Append(';');
            var pred = odstavec.PredAuto ? 14 : odstavec.Pred;
            var za = odstavec.ZaAuto ? 14 : odstavec.Za;
            if (pred != 0) css.Append("padding-top:").Append(Pt(pred)).Append(';');
            if (odstavec.SpodniCara is { } cara)
            {
                css.Append("border-bottom:").Append(cara).Append(";padding-bottom:").Append(Pt(odstavec.SpodniCaraMezera)).Append(';');
            }

            if (za != 0) css.Append("margin-bottom:").Append(Pt(za)).Append(';');

            if (odstavec.Zarovnani != "left") css.Append("text-align:").Append(odstavec.Zarovnani).Append(';');
            if (odstavec.Zarovnani == "justify")
            {
                // Word smrští mezery řádku do bloku až o čtvrtinu, vejde-li se tím další slovo; roztažení
                // do bloku pak řádek vyplní stejně jako ve Wordu.
                css.Append("word-spacing:").Append(Pt(-Pismo(znacka.Pismo).Mezera * znacka.Velikost / 4)).Append(';');
            }
            if (odstavec.NovaStrana) css.Append("break-before:page;");
            if (odstavec.DrzetSDalsim) css.Append("break-after:avoid;");
            if (!odstavec.Vdovy) css.Append("orphans:1;widows:1;");
            if (vlastniZarazky) css.Append("position:relative;");
            else if (p.Descendants<TabChar>().Any()) css.Append("tab-size:").Append(Pt(_tabulator)).Append(';');

            var obsah = new StringBuilder();
            if (lvl is not null) Cislo(obsah, numId!.Value, uroven, lvl, znacka, odstavec, radek);
            Behy(p, retez, znacka, radek, odstavec, vlastniZarazky, obsah);

            html.Append("<p style=\"").Append(css).Append("\">");
            html.Append(obsah.Length == 0 ? "<br/>" : obsah.ToString());
            html.Append("</p>");
        }

        // ---------- číslování ----------

        private void Cislo(StringBuilder html, int numId, int uroven, Uroven lvl, BehV znacka, OdstavecV odstavec, double radek)
        {
            var instance = _instance[numId];
            var urovne = _abstraktni[instance.Abstraktni];
            int Start(int u) => instance.Starty.TryGetValue(u, out var s) ? s : urovne.TryGetValue(u, out var x) ? x.Start : 0;

            // Instance s přepsaným startem čísluje samostatně, ostatní sdílí počítadlo abstraktní definice.
            var klic = instance.Starty.Count > 0 ? $"n{numId}" : $"a{instance.Abstraktni}";
            if (!_citace.TryGetValue(klic, out var citac))
            {
                citac = Enumerable.Range(0, 9).Select(u => Start(u) - 1).ToArray();
                _citace[klic] = citac;
            }

            citac[uroven]++;
            for (var u = uroven + 1; u < citac.Length; u++) citac[u] = Start(u) - 1;

            var beh = znacka.Kopie();
            Prevezmi(beh, lvl.Beh);
            var text = lvl.Text;
            if (lvl.Format != "bullet")
            {
                for (var u = 8; u >= 0; u--)
                {
                    var format = urovne.TryGetValue(u, out var x) ? x.Format : "decimal";
                    text = text.Replace($"%{u + 1}", FormatCisla(citac[u], format));
                }
            }

            var css = new StringBuilder();
            string cislo;
            if (text.Length == 1 && Symboly.TryGetValue((beh.Pismo, text[0]), out var symbol))
            {
                var (znak, tvar) = symbol;
                var em = beh.Velikost;
                css.Append("display:inline-block;overflow:hidden;width:").Append(Pt(tvar.Rozmer * em))
                    .Append(";height:").Append(Pt(tvar.Rozmer * em))
                    .Append(";margin-left:").Append(Pt(tvar.Vlevo * em))
                    .Append(";margin-right:").Append(Pt((tvar.Posun - tvar.Vlevo - tvar.Rozmer) * em))
                    .Append(";vertical-align:").Append(Pt(tvar.Dole * em))
                    .Append(";background-color:").Append(beh.Barva)
                    .Append(tvar.Kruh ? ";border-radius:50%" : string.Empty)
                    .Append(";font-size:0;line-height:0;text-indent:0");
                cislo = $"<span class=\"w-cislo\" style=\"{css}\">{Esc(znak.ToString())}</span>";
            }
            else
            {
                foreach (var znak in text.Where(z => z >= '\uF000' && z <= '\uF0FF'))
                {
                    Nezname.Add($"znak {beh.Pismo} U+{(int)znak:X4}");
                }

                PismoCss(css, beh);
                if (beh.Podtrzeni) css.Append("text-decoration:underline;");
                css.Append("text-indent:0");
                cislo = $"<span class=\"w-cislo\" style=\"{css}\">{Esc(text)}</span>";
            }

            // Písmo značky (odrážka písmem Symbol) zvyšuje ve Wordu řádek, a to nad textem — větší horní dotah.
            var navic = VyskaRadku(beh, odstavec) - radek;
            var nad = navic > 0.0005 ? $"padding-top:{Pt(navic)};" : string.Empty;

            // Číslo začíná na začátku prvního řádku, text pokračuje na levém okraji (implicitní zarážka předsazení).
            var mezera = odstavec.PrvniRadek < 0 ? -odstavec.PrvniRadek : _tabulator;
            if (lvl.Zarovnani is "right" or "center")
            {
                html.Append("<span class=\"w-misto\" style=\"").Append(nad).Append("display:inline-flex;justify-content:")
                    .Append(lvl.Zarovnani == "right" ? "flex-end" : "center")
                    .Append(";width:0;margin-right:").Append(Pt(mezera)).Append("\">").Append(cislo).Append("</span>");
            }
            else
            {
                if (lvl.Zarovnani != "left") Nezname.Add($"lvlJc={lvl.Zarovnani}");
                html.Append("<span class=\"w-misto\" style=\"").Append(nad).Append("display:inline-block;min-width:").Append(Pt(mezera))
                    .Append(";text-indent:0\">").Append(cislo).Append("</span>");
            }
        }

        private string FormatCisla(int cislo, string format) => format switch
        {
            "decimal" => cislo.ToString(Inv),
            "upperRoman" => Rimsky(cislo),
            "lowerRoman" => Rimsky(cislo).ToLowerInvariant(),
            "upperLetter" => Pismeno(cislo),
            "lowerLetter" => Pismeno(cislo).ToLowerInvariant(),
            "none" => string.Empty,
            _ => Neznamy(format, cislo),
        };

        private string Neznamy(string format, int cislo)
        {
            Nezname.Add($"numFmt={format}");
            return cislo.ToString(Inv);
        }

        private static string Rimsky(int cislo)
        {
            var hodnoty = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            var znaky = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var text = new StringBuilder();
            for (var i = 0; i < hodnoty.Length; i++)
            {
                while (cislo >= hodnoty[i])
                {
                    text.Append(znaky[i]);
                    cislo -= hodnoty[i];
                }
            }

            return text.ToString();
        }

        // Word: A … Z, pak AA … ZZ, AAA …
        private static string Pismeno(int cislo)
            => cislo <= 0 ? string.Empty : new string((char)('A' + (cislo - 1) % 26), (cislo - 1) / 26 + 1);

        // ---------- běhy ----------

        private void Behy(Paragraph p, List<Style> retez, BehV znacka, double radek, OdstavecV odstavec, bool vlastniZarazky, StringBuilder html)
        {
            // Vlastní zarážky: úseky za tabulátory se umístí na zarážky (zarážky Wordu ruší výchozí před sebou).
            var zarazky = new List<Zarazka>();
            if (vlastniZarazky)
            {
                zarazky.AddRange(odstavec.Zarazky.OrderBy(z => z.Pozice));
                var posledni = zarazky[^1].Pozice;
                for (var pozice = _tabulator; pozice < 1000; pozice += _tabulator)
                {
                    if (pozice > posledni + 0.01) zarazky.Add(new Zarazka(pozice, "left"));
                }
            }

            var useky = new List<(Zarazka? Zarazka, StringBuilder Html)> { (null, new StringBuilder()) };
            var aktualni = 0d;
            string? pole = null;           // instrukce rozpracovaného pole
            var vysledekPole = false;      // jsme ve výsledku pole, které se nahradilo
            BehV? behPole = null;

            foreach (var beh in Behy(p))
            {
                var vlastnosti = BehVlastnosti(beh, retez);
                foreach (var obsah in beh.ChildElements)
                {
                    switch (obsah)
                    {
                        case FieldChar znak when znak.FieldCharType?.Value == FieldCharValues.Begin:
                            pole = string.Empty;
                            behPole = vlastnosti;
                            break;
                        case FieldCode instrukce when pole is not null && !vysledekPole:
                            pole += instrukce.Text;
                            break;
                        case FieldChar znak when znak.FieldCharType?.Value == FieldCharValues.Separate:
                            vysledekPole = Pole(pole, behPole ?? vlastnosti, znacka, radek, odstavec, useky[^1].Html);
                            pole = vysledekPole ? pole : null;
                            break;
                        case FieldChar znak when znak.FieldCharType?.Value == FieldCharValues.End:
                            if (pole is not null && !vysledekPole) Pole(pole, behPole ?? vlastnosti, znacka, radek, odstavec, useky[^1].Html);
                            pole = null;
                            vysledekPole = false;
                            break;
                        case Text text when pole is null:
                            Beh(useky[^1].Html, text.Text, vlastnosti, znacka, radek, odstavec);
                            break;
                        case TabChar when pole is null:
                            if (vlastniZarazky)
                            {
                                var zarazka = zarazky.FirstOrDefault(z => z.Pozice > aktualni + 0.01);
                                aktualni = zarazka.Pozice;
                                useky.Add((zarazka, new StringBuilder()));
                            }
                            else
                            {
                                Beh(useky[^1].Html, "\t", vlastnosti, znacka, radek, odstavec);
                            }
                            break;
                        case Break zalomeni when pole is null:
                            if (zalomeni.Type?.Value is { } druh && druh != BreakValues.TextWrapping) Nezname.Add($"br={zalomeni.Type.InnerText}");
                            useky[^1].Html.Append("<br/>");
                            break;
                    }
                }
            }

            html.Append(useky[0].Html);
            foreach (var (zarazka, usek) in useky.Skip(1))
            {
                if (usek.Length == 0 || zarazka is not { } z) continue;
                html.Append("<span style=\"position:absolute;left:").Append(Pt(z.Pozice));
                if (z.Druh != "left") html.Append(";transform:translateX(").Append(z.Druh == "right" ? "-100%" : "-50%").Append(')');
                html.Append("\">").Append(usek).Append("</span>");
            }
        }

        /// <summary>Běhy odstavce včetně vnořených v ovládacích prvcích, odkazech a jednoduchých polích.</summary>
        private IEnumerable<Run> Behy(OpenXmlElement rodic)
        {
            foreach (var potomek in rodic.ChildElements)
            {
                switch (potomek)
                {
                    case Run beh: yield return beh; break;
                    case SdtRun sdt when sdt.SdtContentRun is { } obsah:
                        foreach (var beh in Behy(obsah)) yield return beh;
                        break;
                    case Hyperlink odkaz:
                        foreach (var beh in Behy(odkaz)) yield return beh;
                        break;
                    case SimpleField pole:
                        Nezname.Add("fldSimple");
                        foreach (var beh in Behy(pole)) yield return beh;
                        break;
                }
            }
        }

        private BehV BehVlastnosti(Run beh, List<Style> retezOdstavce)
        {
            var vlastnosti = VychoziBeh();
            foreach (var styl in retezOdstavce) Prevezmi(vlastnosti, styl.StyleRunProperties);
            foreach (var styl in Retez(beh.RunProperties?.RunStyle?.Val?.Value ?? _vychoziZnakovy)) Prevezmi(vlastnosti, styl.StyleRunProperties);
            Prevezmi(vlastnosti, beh.RunProperties);
            return vlastnosti;
        }

        /// <summary>Pole PAGE a NUMPAGES vyplní prohlížeč při lámání stránek; jiná pole zůstanou s výsledkem z Wordu.</summary>
        private bool Pole(string? instrukce, BehV beh, BehV znacka, double radek, OdstavecV odstavec, StringBuilder html)
        {
            var nazev = (instrukce ?? string.Empty).Trim().Split(' ', 2)[0].ToUpperInvariant();
            var trida = nazev switch { "PAGE" => "pageNumber", "NUMPAGES" => "totalPages", _ => null };
            if (trida is null) return false;

            html.Append("<span style=\"").Append(RozdilBehu(beh, znacka, VyskaRadku(beh, odstavec), radek))
                .Append("\"><span class=\"").Append(trida).Append("\"></span></span>");
            return true;
        }

        private void Beh(StringBuilder html, string text, BehV beh, BehV znacka, double radek, OdstavecV odstavec)
        {
            if (text.Length == 0) return;

            // Běh jen z mezer a tabulátorů řádek ve Wordu nezvyšuje.
            var bila = string.IsNullOrWhiteSpace(text);
            var styl = RozdilBehu(beh, znacka, bila ? 0 : VyskaRadku(beh, odstavec), radek);
            var otevreni = new StringBuilder();
            var zavreni = new StringBuilder();
            if (styl.Length > 0)
            {
                otevreni.Append("<span style=\"").Append(styl).Append("\">");
                zavreni.Insert(0, "</span>");
            }

            if (beh.Tucne && !znacka.Tucne) { otevreni.Append("<strong>"); zavreni.Insert(0, "</strong>"); }
            if (beh.Kurziva && !znacka.Kurziva) { otevreni.Append("<em>"); zavreni.Insert(0, "</em>"); }
            if (beh.Podtrzeni) { otevreni.Append("<u>"); zavreni.Insert(0, "</u>"); }

            if (otevreni.Length == 0)
            {
                html.Append("<span>").Append(Esc(text)).Append("</span>");
                return;
            }

            html.Append(otevreni).Append(Esc(text)).Append(zavreni);
        }

        /// <summary>CSS běhu tam, kde se liší od odstavce (písmo odstavce = formát jeho konce).</summary>
        private static string RozdilBehu(BehV beh, BehV znacka, double vyska, double radek)
        {
            var css = new StringBuilder();
            if (beh.Pismo != znacka.Pismo) css.Append("font-family:").Append(Pismo(beh.Pismo).Css).Append(';');
            if (beh.Velikost != znacka.Velikost) css.Append("font-size:").Append(Pt(beh.Velikost)).Append(';');
            if (!beh.Tucne && znacka.Tucne) css.Append("font-weight:normal;");
            if (!beh.Kurziva && znacka.Kurziva) css.Append("font-style:normal;");
            if (beh.Barva != znacka.Barva) css.Append("color:").Append(beh.Barva).Append(';');
            if (beh.Prolozeni != znacka.Prolozeni) css.Append("letter-spacing:").Append(Pt(beh.Prolozeni)).Append(';');
            if (vyska == 0) css.Append("line-height:0;");
            else if (Math.Abs(vyska - radek) > 0.0005) css.Append("line-height:").Append(Pt(vyska)).Append(';');
            return css.ToString().TrimEnd(';');
        }

        // ---------- tabulka ----------

        private void Tabulka(Table tabulka, StringBuilder html)
        {
            var tblPr = tabulka.GetFirstChild<TableProperties>();
            var mrizka = tabulka.GetFirstChild<TableGrid>()?.Elements<GridColumn>()
                .Select(s => Tw(s.Width?.Value)).ToList() ?? new List<double>();

            // Výchozí styl tabulky, pak vlastnosti tabulky.
            var cary = new Dictionary<string, string>();
            var okrajeBunek = new Dictionary<string, double> { ["top"] = 0, ["left"] = 5.4, ["bottom"] = 0, ["right"] = 5.4 };
            var odsazeni = 0d;
            foreach (var vlastnosti in new OpenXmlElement?[] { _vychoziTabulka, tblPr })
            {
                if (vlastnosti is null) continue;
                if (vlastnosti.GetFirstChild<TableIndentation>()?.Width?.Value is { } ind) odsazeni = ind / 20d;
                foreach (var cara in vlastnosti.GetFirstChild<TableBorders>()?.ChildElements.OfType<BorderType>() ?? Enumerable.Empty<BorderType>())
                {
                    cary[Strana(cara.LocalName)] = Cara(cara);
                }

                foreach (var okraj in vlastnosti.GetFirstChild<TableCellMarginDefault>()?.ChildElements ?? Enumerable.Empty<OpenXmlElement>())
                {
                    var sirka = okraj.GetAttributes().FirstOrDefault(a => a.LocalName == "w").Value;
                    okrajeBunek[Strana(okraj.LocalName)] = Tw(sirka);
                }
            }

            var sirkaTabulky = tblPr?.TableWidth is { } tblW && tblW.Type?.Value == TableWidthUnitValues.Dxa
                ? Tw(tblW.Width?.Value)
                : mrizka.Sum();
            if (tblPr?.TableWidth?.Type?.Value is { } typ && typ != TableWidthUnitValues.Dxa && typ != TableWidthUnitValues.Auto)
            {
                Nezname.Add($"tblW={tblPr.TableWidth.Type.InnerText}");
            }

            html.Append("<table style=\"width:").Append(Pt(sirkaTabulky));
            if (odsazeni != 0) html.Append(";margin-left:").Append(Pt(odsazeni));
            html.Append("\"><colgroup>");
            foreach (var sloupec in mrizka) html.Append("<col style=\"width:").Append(Pt(sloupec)).Append("\"/>");
            html.Append("</colgroup><tbody>");

            // Nejdřív čáry všech buněk: sdílenou hranu kreslí Word i prohlížeč silnější z obou čar.
            var radky = tabulka.Elements<TableRow>().ToList();
            var bunky = new List<List<(TableCell Bunka, int Rozpeti, Dictionary<string, string> Cary)>>();
            for (var r = 0; r < radky.Count; r++)
            {
                var radek = new List<(TableCell, int, Dictionary<string, string>)>();
                var sloupec = 0;
                foreach (var bunka in radky[r].Elements<TableCell>())
                {
                    var rozpeti = bunka.TableCellProperties?.GridSpan?.Val?.Value ?? 1;
                    radek.Add((bunka, rozpeti, CaryBunky(bunka, cary, prvniRadek: r == 0, posledniRadek: r == radky.Count - 1,
                        prvniSloupec: sloupec == 0, posledniSloupec: sloupec + rozpeti >= mrizka.Count)));
                    sloupec += rozpeti;
                }

                bunky.Add(radek);
            }

            double Nejsilnejsi(int r, string strana) => r < 0 || r >= bunky.Count ? 0 : bunky[r].Max(b => Sirka(b.Cary[strana]));

            for (var r = 0; r < radky.Count; r++)
            {
                var trPr = radky[r].TableRowProperties;
                var css = new StringBuilder();
                if (trPr?.GetFirstChild<TableRowHeight>() is { } vyska)
                {
                    if (vyska.HeightType?.Value == HeightRuleValues.Exact) Nezname.Add("trHeight=exact");
                    // Výška řádku ve Wordu je bez čar; prohlížeči čáry patří do řádku napůl.
                    var cara = Math.Max(Nejsilnejsi(r, "top"), Nejsilnejsi(r - 1, "bottom")) / 2
                               + Math.Max(Nejsilnejsi(r, "bottom"), Nejsilnejsi(r + 1, "top")) / 2;
                    css.Append("height:").Append(Pt((vyska.Val?.Value ?? 0) / 20d + cara)).Append(';');
                }

                if (trPr?.GetFirstChild<CantSplit>() is { } nedelit && (nedelit.Val is null || nedelit.Val.Value == OnOffOnlyValues.On))
                {
                    css.Append("break-inside:avoid;");
                }

                html.Append("<tr style=\"").Append(css.ToString().TrimEnd(';')).Append("\">");
                for (var i = 0; i < bunky[r].Count; i++)
                {
                    var (bunka, rozpeti, caryBunky) = bunky[r][i];
                    var vlevo = Math.Max(Sirka(caryBunky["left"]), i > 0 ? Sirka(bunky[r][i - 1].Cary["right"]) : 0);
                    var vpravo = Math.Max(Sirka(caryBunky["right"]), i + 1 < bunky[r].Count ? Sirka(bunky[r][i + 1].Cary["left"]) : 0);
                    Bunka(bunka, html, caryBunky, okrajeBunek, vlevo, vpravo, rozpeti);
                }

                html.Append("</tr>");
            }

            html.Append("</tbody></table>");
        }

        private static string Strana(string nazev) => nazev switch
        {
            "start" => "left",
            "end" => "right",
            _ => nazev,
        };

        private static double Sirka(string cara) => cara == "none" ? 0 : double.Parse(cara[..cara.IndexOf("pt", StringComparison.Ordinal)], Inv);

        /// <summary>Čáry buňky: vlastní, jinak vnější nebo vnitřní čáry tabulky podle polohy buňky.</summary>
        private Dictionary<string, string> CaryBunky(TableCell bunka, Dictionary<string, string> cary,
            bool prvniRadek, bool posledniRadek, bool prvniSloupec, bool posledniSloupec)
        {
            var vlastni = new Dictionary<string, string>();
            foreach (var cara in bunka.TableCellProperties?.TableCellBorders?.ChildElements.OfType<BorderType>() ?? Enumerable.Empty<BorderType>())
            {
                vlastni[Strana(cara.LocalName)] = Cara(cara);
            }

            string Hrana(string strana, bool naOkraji, string vnitrni)
                => vlastni.TryGetValue(strana, out var c) ? c
                    : cary.TryGetValue(naOkraji ? strana : vnitrni, out var t) ? t : "none";

            return new Dictionary<string, string>
            {
                ["top"] = Hrana("top", prvniRadek, "insideH"),
                ["right"] = Hrana("right", posledniSloupec, "insideV"),
                ["bottom"] = Hrana("bottom", posledniRadek, "insideH"),
                ["left"] = Hrana("left", prvniSloupec, "insideV"),
            };
        }

        /// <summary>
        /// Buňka. Ve Wordu má text šířku sloupce bez okrajů buňky a čára leží na hraně mřížky; prohlížeč
        /// počítá polovinu čáry dovnitř buňky, proto se okraj buňky o ni zmenší.
        /// </summary>
        private void Bunka(TableCell bunka, StringBuilder html, Dictionary<string, string> cary, Dictionary<string, double> okraje,
            double caraVlevo, double caraVpravo, int rozpeti)
        {
            var tcPr = bunka.TableCellProperties;
            var css = new StringBuilder()
                .Append("border-top:").Append(cary["top"]).Append(';')
                .Append("border-right:").Append(cary["right"]).Append(';')
                .Append("border-bottom:").Append(cary["bottom"]).Append(';')
                .Append("border-left:").Append(cary["left"]).Append(';')
                .Append("padding:").Append(Pt(okraje["top"])).Append(' ').Append(Pt(Math.Max(0, okraje["right"] - caraVpravo / 2))).Append(' ')
                .Append(Pt(okraje["bottom"])).Append(' ').Append(Pt(Math.Max(0, okraje["left"] - caraVlevo / 2))).Append(';');

            var svisle = tcPr?.TableCellVerticalAlignment?.Val?.Value;
            css.Append("vertical-align:").Append(
                svisle == TableVerticalAlignmentValues.Center ? "middle"
                : svisle == TableVerticalAlignmentValues.Bottom ? "bottom" : "top");

            if (tcPr?.Shading is { } stin)
            {
                if (stin.Val?.Value is { } vzor && vzor != ShadingPatternValues.Clear && vzor != ShadingPatternValues.Nil)
                {
                    Nezname.Add($"shd={stin.Val.InnerText}");
                }

                if (stin.Fill?.Value is { } vypln && vypln != "auto") css.Append(";background-color:#").Append(vypln);
            }

            html.Append("<td");
            if (rozpeti > 1) html.Append(" colspan=\"").Append(rozpeti.ToString(Inv)).Append('"');
            html.Append(" style=\"").Append(css).Append("\">");
            Bloky(bunka.ChildElements, html);
            html.Append("</td>");
        }

        // ---------- stránka, záhlaví, zápatí ----------

        private static Stranka Stranka(SectionProperties oddil)
        {
            var velikost = oddil.GetFirstChild<PageSize>();
            var okraje = oddil.GetFirstChild<PageMargin>();
            return new Stranka(
                (velikost?.Width?.Value ?? 11906) / 20d,
                (velikost?.Height?.Value ?? 16838) / 20d,
                (okraje?.Top?.Value ?? 1440) / 20d,
                (okraje?.Right?.Value ?? 1440) / 20d,
                (okraje?.Bottom?.Value ?? 1440) / 20d,
                (okraje?.Left?.Value ?? 1440) / 20d,
                (okraje?.Header?.Value ?? 720) / 20d,
                (okraje?.Footer?.Value ?? 720) / 20d);
        }

        /// <summary>
        /// Záhlaví nebo zápatí jako šablona prohlížeče. Šablona leží přes celou šířku stránky v pásu okraje;
        /// záhlaví začíná ve vzdálenosti záhlaví od horní hrany, zápatí končí ve vzdálenosti zápatí od dolní.
        /// </summary>
        private string Sablona(SectionProperties oddil, Stranka stranka, bool zahlavi)
        {
            var obsah = new StringBuilder();
            if (zahlavi)
            {
                var odkaz = oddil.Elements<HeaderReference>().FirstOrDefault(h => h.Type?.Value is null || h.Type.Value == HeaderFooterValues.Default);
                if (odkaz?.Id?.Value is { } id && _hlavni.GetPartById(id) is HeaderPart cast && cast.Header is { } hlavicka)
                {
                    Bloky(hlavicka.ChildElements, obsah);
                }
            }
            else
            {
                var odkaz = oddil.Elements<FooterReference>().FirstOrDefault(h => h.Type?.Value is null || h.Type.Value == HeaderFooterValues.Default);
                if (odkaz?.Id?.Value is { } id && _hlavni.GetPartById(id) is FooterPart cast && cast.Footer is { } paticka)
                {
                    Bloky(paticka.ChildElements, obsah);
                }
            }

            var poloha = zahlavi
                ? $"top:{Pt(stranka.Zahlavi)}"
                : $"bottom:{Pt(stranka.Zapati)}";
            return new StringBuilder()
                .Append("<style>body{margin:0;").Append(ZakladTela).Append('}').Append(ZakladniStyl).Append("</style>")
                .Append("<div style=\"position:absolute;left:").Append(Pt(stranka.Vlevo)).Append(";width:").Append(Pt(stranka.Sloupec))
                .Append(';').Append(poloha).Append("\">").Append(obsah).Append("</div>")
                .ToString();
        }
    }
}
