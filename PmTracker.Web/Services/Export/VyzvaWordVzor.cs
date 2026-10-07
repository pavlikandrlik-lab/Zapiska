using System.Security;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PmTracker.Web.Services.Export;

/// <summary>
/// Části Wordu výzvy převzaté 1:1 z XML vzoru <c>2027xxxx_N_8201_Vyzva_c_x_2027_EIS.docx</c>
/// (word/document.xml, header1.xml, footer1.xml, styles.xml, numbering.xml, settings.xml;
/// uživatel 2026-10-07: „co je ve vzoru vidět, musí být ve vygenerovaném Wordu stejné“).
/// Proti vzoru jsou odstraněné jen identifikátory revizí (rsid), značky kontroly pravopisu,
/// komentáře autora vzoru a zvýraznění; běhy se stejným formátem jsou sloučené. Prázdné řádky
/// zůstávají prázdnými odstavci jako ve vzoru — nikdy ale těsně před sekcí na nové stránce, kde by
/// při konci stránky vyrobily prázdnou stránku. Údaje konkrétní výzvy (č. j., datum, termín) zůstávají
/// prázdné k doplnění ve Wordu. Při změně vzoru XML znovu zkopírovat, ne dolaďovat formát ručně.
/// Tabulky jsou v <c>VyzvaWordVzor.Tabulky.cs</c>.
/// </summary>
internal static partial class VyzvaWordVzor
{
    /// <summary>Styl Zápatí vzoru.</summary>
    public const string StylZapati = "Zpat";

    /// <summary>Číslování vzoru: sekce 1.–7., požadavky v sekci 1 a v sekci 2 (římsky).</summary>
    public const int NumSekce = 3;
    public const int NumPozadavkySekce1 = 47;
    public const int NumPozadavkySekce2 = 48;

    /// <summary>Abstraktní číslování seznamů z textu požadavku (odrážky a číslovaný seznam vzoru).</summary>
    public const int AbsOdrazky = 12;
    public const int AbsCislovany = 41;

    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string OdsazeniNadpisuSekce1 = """<w:ind w:left="426" w:hanging="284"/>""";
    private const string Tnr = """<w:rFonts w:ascii="Times New Roman" w:hAnsi="Times New Roman" w:cs="Times New Roman"/>""";

    // document.xml: hlavička úřadu, prázdný řádek, „Čj. … V Praze dne“, prázdný řádek,
    // nadpis výzvy (text doplní HlavickaUradu), prázdný řádek.
    private static readonly string[] HlavickaXml =
    {
        """<w:p><w:pPr><w:pStyle w:val="Zhlav"/><w:jc w:val="center"/><w:outlineLvl w:val="0"/><w:rPr><w:b/><w:spacing w:val="40"/><w:sz w:val="32"/><w:szCs w:val="32"/></w:rPr></w:pPr><w:r><w:rPr><w:b/><w:spacing w:val="40"/><w:sz w:val="32"/><w:szCs w:val="32"/></w:rPr><w:t>Sekce vyzbrojování a akvizic Ministerstva obrany</w:t></w:r></w:p>""",
        """<w:p><w:pPr><w:pStyle w:val="Zhlav"/><w:jc w:val="center"/><w:outlineLvl w:val="0"/><w:rPr><w:b/></w:rPr></w:pPr><w:r><w:rPr><w:b/></w:rPr><w:t>odbor komunikačních a informačních systémů</w:t></w:r></w:p>""",
        """<w:p><w:pPr><w:pStyle w:val="Zhlav"/><w:pBdr><w:bottom w:val="single" w:sz="12" w:space="1" w:color="auto"/></w:pBdr><w:jc w:val="center"/><w:rPr><w:sz w:val="20"/><w:szCs w:val="20"/></w:rPr></w:pPr><w:r><w:rPr><w:sz w:val="20"/><w:szCs w:val="20"/></w:rPr><w:t>náměstí Svobody 471/4, Praha 6, PSČ 160 01, datová schránka hjyaavk</w:t></w:r></w:p>""",
        """<w:p><w:pPr><w:tabs><w:tab w:val="left" w:pos="5040"/></w:tabs><w:jc w:val="both"/></w:pPr></w:p>""",
        """<w:p><w:pPr><w:tabs><w:tab w:val="left" w:pos="5040"/></w:tabs><w:jc w:val="both"/></w:pPr><w:r><w:t>Čj.</w:t></w:r><w:r><w:tab/></w:r><w:r><w:tab/></w:r><w:r><w:tab/><w:t>V Praze dne</w:t></w:r></w:p>""",
        """<w:p><w:pPr><w:tabs><w:tab w:val="left" w:pos="5040"/></w:tabs><w:jc w:val="both"/></w:pPr></w:p>""",
        """<w:p><w:pPr><w:pStyle w:val="Nadpis2"/><w:keepNext w:val="0"/><w:overflowPunct/><w:autoSpaceDE/><w:adjustRightInd/><w:jc w:val="both"/><w:rPr><w:bCs/><w:szCs w:val="24"/></w:rPr></w:pPr><w:r><w:rPr><w:bCs/><w:szCs w:val="24"/></w:rPr><w:t xml:space="preserve"></w:t></w:r></w:p>""",
        """<w:p><w:pPr><w:jc w:val="both"/></w:pPr></w:p>""",
    };

    private const int IndexNadpisu = 6;

    private const string PodrobneNavrhy =
        "Podrobné návrhy požadavků jsou součástí příslušného protokolu (HotLine – uvedená v tabulce shora) "
        + "a specifikace. Byly analyzovány dodavatelem a jejich užitnost je posuzována zadavatelem, vedením "
        + "projektu EIS, Řídícím výborem FIS, případně dalšími odborníky. Požadavky jsou posuzovány jednotlivými "
        + "vedoucími subsystémů FIS/ISSP a příslušnými metodiky. Požadavky jsou schváleny vedením projektu "
        + "FIS/ISSP (VP EIS). Čísla úkolů jsou uvedena také v souhrnné tabulce shora. Dále je uvedena stručná "
        + "anotace požadavků.";

    // header1.xml — číslo jednací vzoru vynechané, doplní se ve Wordu (spec B2).
    private const string ZahlaviXml =
        """<w:hdr><w:p><w:pPr><w:pStyle w:val="Zhlav"/><w:jc w:val="right"/></w:pPr><w:r><w:t xml:space="preserve">Příloha č.1 k Čj. MO </w:t></w:r></w:p></w:hdr>""";

    // footer1.xml — číslo stránky v ovládacím prvku galerie čísel stránek a prázdný odstavec pod ním.
    private const string ZapatiXml =
        """<w:ftr><w:p><w:pPr><w:pStyle w:val="Zpat"/><w:ind w:left="1080"/><w:jc w:val="center"/><w:rPr><w:sz w:val="20"/></w:rPr></w:pPr><w:sdt><w:sdtPr><w:rPr><w:sz w:val="20"/></w:rPr><w:id w:val="-1639724445"/><w:docPartObj><w:docPartGallery w:val="Page Numbers (Bottom of Page)"/><w:docPartUnique/></w:docPartObj></w:sdtPr><w:sdtContent><w:r><w:rPr><w:sz w:val="20"/></w:rPr><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:rPr><w:sz w:val="20"/></w:rPr><w:instrText>PAGE   \* MERGEFORMAT</w:instrText></w:r><w:r><w:rPr><w:sz w:val="20"/></w:rPr><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:rPr><w:sz w:val="20"/></w:rPr><w:t>2</w:t></w:r><w:r><w:rPr><w:sz w:val="20"/></w:rPr><w:fldChar w:fldCharType="end"/></w:r></w:sdtContent></w:sdt></w:p><w:p><w:pPr><w:pStyle w:val="Zpat"/></w:pPr></w:p></w:ftr>""";

    // numbering.xml — sekce 1.–7. (18), požadavky v sekci 1 (4) a 2 (10), odrážky (12), číslovaný seznam (41).
    private static readonly string[] AbstraktniCislovaniXml =
    {
        """<w:abstractNum w:abstractNumId="18"><w:multiLevelType w:val="hybridMultilevel"/><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%1."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="360" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:hint="default"/></w:rPr></w:lvl><w:lvl w:ilvl="1" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%2."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="1080" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="2" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%3."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="1800" w:hanging="180"/></w:pPr></w:lvl><w:lvl w:ilvl="3" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%4."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="2520" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="4" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%5."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="3240" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="5" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%6."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="3960" w:hanging="180"/></w:pPr></w:lvl><w:lvl w:ilvl="6" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%7."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="4680" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="7" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%8."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="5400" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="8" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%9."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="6120" w:hanging="180"/></w:pPr></w:lvl></w:abstractNum>""",
        """<w:abstractNum w:abstractNumId="4"><w:multiLevelType w:val="hybridMultilevel"/><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="upperRoman"/><w:lvlText w:val="%1."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="360" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:hint="default"/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr></w:lvl><w:lvl w:ilvl="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%2."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="1080" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="2" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%3."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="1800" w:hanging="180"/></w:pPr></w:lvl><w:lvl w:ilvl="3" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%4."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="2520" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="4" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%5."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="3240" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="5" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%6."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="3960" w:hanging="180"/></w:pPr></w:lvl><w:lvl w:ilvl="6" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%7."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="4680" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="7" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%8."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="5400" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="8" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%9."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="6120" w:hanging="180"/></w:pPr></w:lvl></w:abstractNum>""",
        """<w:abstractNum w:abstractNumId="10"><w:multiLevelType w:val="hybridMultilevel"/><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="upperRoman"/><w:lvlText w:val="%1."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="360" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:hint="default"/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr></w:lvl><w:lvl w:ilvl="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%2."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="1080" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="2" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%3."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="1800" w:hanging="180"/></w:pPr></w:lvl><w:lvl w:ilvl="3" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%4."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="2520" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="4" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%5."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="3240" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="5" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%6."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="3960" w:hanging="180"/></w:pPr></w:lvl><w:lvl w:ilvl="6" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%7."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="4680" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="7" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%8."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="5400" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="8" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%9."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="6120" w:hanging="180"/></w:pPr></w:lvl></w:abstractNum>""",
        """<w:abstractNum w:abstractNumId="12"><w:multiLevelType w:val="hybridMultilevel"/><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val=""/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="720" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Symbol" w:hAnsi="Symbol" w:hint="default"/></w:rPr></w:lvl><w:lvl w:ilvl="1" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="o"/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="1440" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Courier New" w:hAnsi="Courier New" w:cs="Courier New" w:hint="default"/></w:rPr></w:lvl><w:lvl w:ilvl="2" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val=""/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="2160" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Wingdings" w:hAnsi="Wingdings" w:hint="default"/></w:rPr></w:lvl><w:lvl w:ilvl="3" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val=""/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="2880" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Symbol" w:hAnsi="Symbol" w:hint="default"/></w:rPr></w:lvl><w:lvl w:ilvl="4" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="o"/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="3600" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Courier New" w:hAnsi="Courier New" w:cs="Courier New" w:hint="default"/></w:rPr></w:lvl><w:lvl w:ilvl="5" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val=""/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="4320" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Wingdings" w:hAnsi="Wingdings" w:hint="default"/></w:rPr></w:lvl><w:lvl w:ilvl="6" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val=""/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="5040" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Symbol" w:hAnsi="Symbol" w:hint="default"/></w:rPr></w:lvl><w:lvl w:ilvl="7" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="o"/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="5760" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Courier New" w:hAnsi="Courier New" w:cs="Courier New" w:hint="default"/></w:rPr></w:lvl><w:lvl w:ilvl="8" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val=""/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="6480" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:ascii="Wingdings" w:hAnsi="Wingdings" w:hint="default"/></w:rPr></w:lvl></w:abstractNum>""",
        """<w:abstractNum w:abstractNumId="41"><w:multiLevelType w:val="hybridMultilevel"/><w:lvl w:ilvl="0"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%1."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="720" w:hanging="360"/></w:pPr><w:rPr><w:rFonts w:hint="default"/></w:rPr></w:lvl><w:lvl w:ilvl="1" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%2."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="1440" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="2" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%3."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="2160" w:hanging="180"/></w:pPr></w:lvl><w:lvl w:ilvl="3" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%4."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="2880" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="4" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%5."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="3600" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="5" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%6."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="4320" w:hanging="180"/></w:pPr></w:lvl><w:lvl w:ilvl="6" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="decimal"/><w:lvlText w:val="%7."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="5040" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="7" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerLetter"/><w:lvlText w:val="%8."/><w:lvlJc w:val="left"/><w:pPr><w:ind w:left="5760" w:hanging="360"/></w:pPr></w:lvl><w:lvl w:ilvl="8" w:tentative="1"><w:start w:val="1"/><w:numFmt w:val="lowerRoman"/><w:lvlText w:val="%9."/><w:lvlJc w:val="right"/><w:pPr><w:ind w:left="6480" w:hanging="180"/></w:pPr></w:lvl></w:abstractNum>""",
    };

    // theme/theme1.xml — motiv sady Office: písmo Calibri pro tabulku licencí (písmo motivu).
    private const string MotivXml =
        """<a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Motiv sady Office"><a:themeElements><a:clrScheme name="Kancelář"><a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1><a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1><a:dk2><a:srgbClr val="1F497D"/></a:dk2><a:lt2><a:srgbClr val="EEECE1"/></a:lt2><a:accent1><a:srgbClr val="4F81BD"/></a:accent1><a:accent2><a:srgbClr val="C0504D"/></a:accent2><a:accent3><a:srgbClr val="9BBB59"/></a:accent3><a:accent4><a:srgbClr val="8064A2"/></a:accent4><a:accent5><a:srgbClr val="4BACC6"/></a:accent5><a:accent6><a:srgbClr val="F79646"/></a:accent6><a:hlink><a:srgbClr val="0000FF"/></a:hlink><a:folHlink><a:srgbClr val="800080"/></a:folHlink></a:clrScheme><a:fontScheme name="Kancelář"><a:majorFont><a:latin typeface="Cambria"/><a:ea typeface=""/><a:cs typeface=""/><a:font script="Jpan" typeface="ＭＳ ゴシック"/><a:font script="Hang" typeface="맑은 고딕"/><a:font script="Hans" typeface="宋体"/><a:font script="Hant" typeface="新細明體"/><a:font script="Arab" typeface="Times New Roman"/><a:font script="Hebr" typeface="Times New Roman"/><a:font script="Thai" typeface="Angsana New"/><a:font script="Ethi" typeface="Nyala"/><a:font script="Beng" typeface="Vrinda"/><a:font script="Gujr" typeface="Shruti"/><a:font script="Khmr" typeface="MoolBoran"/><a:font script="Knda" typeface="Tunga"/><a:font script="Guru" typeface="Raavi"/><a:font script="Cans" typeface="Euphemia"/><a:font script="Cher" typeface="Plantagenet Cherokee"/><a:font script="Yiii" typeface="Microsoft Yi Baiti"/><a:font script="Tibt" typeface="Microsoft Himalaya"/><a:font script="Thaa" typeface="MV Boli"/><a:font script="Deva" typeface="Mangal"/><a:font script="Telu" typeface="Gautami"/><a:font script="Taml" typeface="Latha"/><a:font script="Syrc" typeface="Estrangelo Edessa"/><a:font script="Orya" typeface="Kalinga"/><a:font script="Mlym" typeface="Kartika"/><a:font script="Laoo" typeface="DokChampa"/><a:font script="Sinh" typeface="Iskoola Pota"/><a:font script="Mong" typeface="Mongolian Baiti"/><a:font script="Viet" typeface="Times New Roman"/><a:font script="Uigh" typeface="Microsoft Uighur"/></a:majorFont><a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/><a:font script="Jpan" typeface="ＭＳ 明朝"/><a:font script="Hang" typeface="맑은 고딕"/><a:font script="Hans" typeface="宋体"/><a:font script="Hant" typeface="新細明體"/><a:font script="Arab" typeface="Arial"/><a:font script="Hebr" typeface="Arial"/><a:font script="Thai" typeface="Cordia New"/><a:font script="Ethi" typeface="Nyala"/><a:font script="Beng" typeface="Vrinda"/><a:font script="Gujr" typeface="Shruti"/><a:font script="Khmr" typeface="DaunPenh"/><a:font script="Knda" typeface="Tunga"/><a:font script="Guru" typeface="Raavi"/><a:font script="Cans" typeface="Euphemia"/><a:font script="Cher" typeface="Plantagenet Cherokee"/><a:font script="Yiii" typeface="Microsoft Yi Baiti"/><a:font script="Tibt" typeface="Microsoft Himalaya"/><a:font script="Thaa" typeface="MV Boli"/><a:font script="Deva" typeface="Mangal"/><a:font script="Telu" typeface="Gautami"/><a:font script="Taml" typeface="Latha"/><a:font script="Syrc" typeface="Estrangelo Edessa"/><a:font script="Orya" typeface="Kalinga"/><a:font script="Mlym" typeface="Kartika"/><a:font script="Laoo" typeface="DokChampa"/><a:font script="Sinh" typeface="Iskoola Pota"/><a:font script="Mong" typeface="Mongolian Baiti"/><a:font script="Viet" typeface="Arial"/><a:font script="Uigh" typeface="Microsoft Uighur"/></a:minorFont></a:fontScheme><a:fmtScheme name="Kancelář"><a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:gradFill rotWithShape="1"><a:gsLst><a:gs pos="0"><a:schemeClr val="phClr"><a:tint val="50000"/><a:satMod val="300000"/></a:schemeClr></a:gs><a:gs pos="35000"><a:schemeClr val="phClr"><a:tint val="37000"/><a:satMod val="300000"/></a:schemeClr></a:gs><a:gs pos="100000"><a:schemeClr val="phClr"><a:tint val="15000"/><a:satMod val="350000"/></a:schemeClr></a:gs></a:gsLst><a:lin ang="16200000" scaled="1"/></a:gradFill><a:gradFill rotWithShape="1"><a:gsLst><a:gs pos="0"><a:schemeClr val="phClr"><a:shade val="51000"/><a:satMod val="130000"/></a:schemeClr></a:gs><a:gs pos="80000"><a:schemeClr val="phClr"><a:shade val="93000"/><a:satMod val="130000"/></a:schemeClr></a:gs><a:gs pos="100000"><a:schemeClr val="phClr"><a:shade val="94000"/><a:satMod val="135000"/></a:schemeClr></a:gs></a:gsLst><a:lin ang="16200000" scaled="0"/></a:gradFill></a:fillStyleLst><a:lnStyleLst><a:ln w="9525" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"><a:shade val="95000"/><a:satMod val="105000"/></a:schemeClr></a:solidFill><a:prstDash val="solid"/></a:ln><a:ln w="25400" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/></a:ln><a:ln w="38100" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst><a:outerShdw blurRad="40000" dist="20000" dir="5400000" rotWithShape="0"><a:srgbClr val="000000"><a:alpha val="38000"/></a:srgbClr></a:outerShdw></a:effectLst></a:effectStyle><a:effectStyle><a:effectLst><a:outerShdw blurRad="40000" dist="23000" dir="5400000" rotWithShape="0"><a:srgbClr val="000000"><a:alpha val="35000"/></a:srgbClr></a:outerShdw></a:effectLst></a:effectStyle><a:effectStyle><a:effectLst><a:outerShdw blurRad="40000" dist="23000" dir="5400000" rotWithShape="0"><a:srgbClr val="000000"><a:alpha val="35000"/></a:srgbClr></a:outerShdw></a:effectLst><a:scene3d><a:camera prst="orthographicFront"><a:rot lat="0" lon="0" rev="0"/></a:camera><a:lightRig rig="threePt" dir="t"><a:rot lat="0" lon="0" rev="1200000"/></a:lightRig></a:scene3d><a:sp3d><a:bevelT w="63500" h="25400"/></a:sp3d></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:gradFill rotWithShape="1"><a:gsLst><a:gs pos="0"><a:schemeClr val="phClr"><a:tint val="40000"/><a:satMod val="350000"/></a:schemeClr></a:gs><a:gs pos="40000"><a:schemeClr val="phClr"><a:tint val="45000"/><a:shade val="99000"/><a:satMod val="350000"/></a:schemeClr></a:gs><a:gs pos="100000"><a:schemeClr val="phClr"><a:shade val="20000"/><a:satMod val="255000"/></a:schemeClr></a:gs></a:gsLst><a:path path="circle"><a:fillToRect l="50000" t="-80000" r="50000" b="180000"/></a:path></a:gradFill><a:gradFill rotWithShape="1"><a:gsLst><a:gs pos="0"><a:schemeClr val="phClr"><a:tint val="80000"/><a:satMod val="300000"/></a:schemeClr></a:gs><a:gs pos="100000"><a:schemeClr val="phClr"><a:shade val="30000"/><a:satMod val="200000"/></a:schemeClr></a:gs></a:gsLst><a:path path="circle"><a:fillToRect l="50000" t="50000" r="50000" b="50000"/></a:path></a:gradFill></a:bgFillStyleLst></a:fmtScheme></a:themeElements><a:objectDefaults/><a:extraClrSchemeLst/></a:theme>""";

    // styles.xml — výchozí formát a styly, které dokument vzoru používá.
    private const string StylyXml =
        """
        <w:styles><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Times New Roman" w:eastAsia="Times New Roman" w:hAnsi="Times New Roman" w:cs="Times New Roman"/><w:lang w:val="cs-CZ" w:eastAsia="cs-CZ" w:bidi="ar-SA"/></w:rPr></w:rPrDefault><w:pPrDefault/></w:docDefaults>
        <w:style w:type="paragraph" w:default="1" w:styleId="Normln"><w:name w:val="Normal"/><w:qFormat/><w:rPr><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr></w:style>
        <w:style w:type="character" w:default="1" w:styleId="Standardnpsmoodstavce"><w:name w:val="Default Paragraph Font"/><w:uiPriority w:val="1"/><w:semiHidden/><w:unhideWhenUsed/></w:style>
        <w:style w:type="table" w:default="1" w:styleId="Normlntabulka"><w:name w:val="Normal Table"/><w:uiPriority w:val="99"/><w:semiHidden/><w:unhideWhenUsed/><w:tblPr><w:tblInd w:w="0" w:type="dxa"/><w:tblCellMar><w:top w:w="0" w:type="dxa"/><w:left w:w="108" w:type="dxa"/><w:bottom w:w="0" w:type="dxa"/><w:right w:w="108" w:type="dxa"/></w:tblCellMar></w:tblPr></w:style>
        <w:style w:type="numbering" w:default="1" w:styleId="Bezseznamu"><w:name w:val="No List"/><w:uiPriority w:val="99"/><w:semiHidden/><w:unhideWhenUsed/></w:style>
        <w:style w:type="paragraph" w:styleId="Zhlav"><w:name w:val="header"/><w:basedOn w:val="Normln"/><w:link w:val="ZhlavChar"/><w:pPr><w:tabs><w:tab w:val="center" w:pos="4536"/><w:tab w:val="right" w:pos="9072"/></w:tabs></w:pPr></w:style>
        <w:style w:type="character" w:customStyle="1" w:styleId="ZhlavChar"><w:name w:val="Záhlaví Char"/><w:link w:val="Zhlav"/><w:rPr><w:sz w:val="24"/><w:szCs w:val="24"/><w:lang w:val="cs-CZ" w:eastAsia="cs-CZ" w:bidi="ar-SA"/></w:rPr></w:style>
        <w:style w:type="paragraph" w:styleId="Zpat"><w:name w:val="footer"/><w:basedOn w:val="Normln"/><w:link w:val="ZpatChar"/><w:uiPriority w:val="99"/><w:pPr><w:tabs><w:tab w:val="center" w:pos="4536"/><w:tab w:val="right" w:pos="9072"/></w:tabs></w:pPr></w:style>
        <w:style w:type="character" w:customStyle="1" w:styleId="ZpatChar"><w:name w:val="Zápatí Char"/><w:basedOn w:val="Standardnpsmoodstavce"/><w:link w:val="Zpat"/><w:uiPriority w:val="99"/><w:rPr><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr></w:style>
        <w:style w:type="paragraph" w:styleId="Nadpis2"><w:name w:val="heading 2"/><w:basedOn w:val="Normln"/><w:next w:val="Normln"/><w:link w:val="Nadpis2Char"/><w:qFormat/><w:pPr><w:keepNext/><w:overflowPunct w:val="0"/><w:autoSpaceDE w:val="0"/><w:autoSpaceDN w:val="0"/><w:adjustRightInd w:val="0"/><w:outlineLvl w:val="1"/></w:pPr><w:rPr><w:b/><w:szCs w:val="20"/></w:rPr></w:style>
        <w:style w:type="character" w:customStyle="1" w:styleId="Nadpis2Char"><w:name w:val="Nadpis 2 Char"/><w:basedOn w:val="Standardnpsmoodstavce"/><w:link w:val="Nadpis2"/><w:rPr><w:b/><w:sz w:val="24"/></w:rPr></w:style>
        <w:style w:type="paragraph" w:customStyle="1" w:styleId="Default"><w:name w:val="Default"/><w:pPr><w:autoSpaceDE w:val="0"/><w:autoSpaceDN w:val="0"/><w:adjustRightInd w:val="0"/></w:pPr><w:rPr><w:color w:val="000000"/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr></w:style>
        <w:style w:type="paragraph" w:styleId="Odstavecseseznamem"><w:name w:val="List Paragraph"/><w:aliases w:val="List Paragraph (Czech Tourism),List Paragraph1,Nad,Odstavec cíl se seznamem,Odstavec se seznamem5,Odstavec_muj"/><w:basedOn w:val="Normln"/><w:link w:val="OdstavecseseznamemChar"/><w:uiPriority w:val="34"/><w:qFormat/><w:pPr><w:spacing w:after="200" w:line="276" w:lineRule="auto"/><w:ind w:left="720"/></w:pPr><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/><w:sz w:val="22"/><w:szCs w:val="22"/></w:rPr></w:style>
        <w:style w:type="character" w:customStyle="1" w:styleId="OdstavecseseznamemChar"><w:name w:val="Odstavec se seznamem Char"/><w:aliases w:val="List Paragraph (Czech Tourism) Char,List Paragraph1 Char,Nad Char,Odstavec cíl se seznamem Char,Odstavec se seznamem5 Char,Odstavec_muj Char"/><w:basedOn w:val="Standardnpsmoodstavce"/><w:link w:val="Odstavecseseznamem"/><w:uiPriority w:val="34"/><w:locked/><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/><w:sz w:val="22"/><w:szCs w:val="22"/></w:rPr></w:style>
        <w:style w:type="paragraph" w:customStyle="1" w:styleId="paragraph"><w:name w:val="paragraph"/><w:basedOn w:val="Normln"/><w:pPr><w:spacing w:before="100" w:beforeAutospacing="1" w:after="100" w:afterAutospacing="1"/></w:pPr></w:style>
        <w:style w:type="character" w:customStyle="1" w:styleId="normaltextrun"><w:name w:val="normaltextrun"/><w:basedOn w:val="Standardnpsmoodstavce"/></w:style>
        <w:style w:type="character" w:customStyle="1" w:styleId="tabchar"><w:name w:val="tabchar"/><w:basedOn w:val="Standardnpsmoodstavce"/></w:style>
        <w:style w:type="character" w:customStyle="1" w:styleId="eop"><w:name w:val="eop"/><w:basedOn w:val="Standardnpsmoodstavce"/></w:style>
        </w:styles>
        """;

    // settings.xml — položky ovlivňující sazbu. Vynechané: zoom, revize (rsids), poznámky pod
    // čarou (dokument je nemá), vkládání písem, odstranění osobních údajů, ID dokumentu.
    private const string NastaveniXml =
        """<w:settings><w:defaultTabStop w:val="708"/><w:hyphenationZone w:val="425"/><w:noPunctuationKerning/><w:characterSpacingControl w:val="doNotCompress"/><w:compat><w:compatSetting w:name="compatibilityMode" w:uri="http://schemas.microsoft.com/office/word" w:val="15"/><w:compatSetting w:name="overrideTableStyleFontSizeAndJustification" w:uri="http://schemas.microsoft.com/office/word" w:val="1"/><w:compatSetting w:name="enableOpenTypeFeatures" w:uri="http://schemas.microsoft.com/office/word" w:val="1"/><w:compatSetting w:name="doNotFlipMirrorIndents" w:uri="http://schemas.microsoft.com/office/word" w:val="1"/><w:compatSetting w:name="differentiateMultirowTableHeaders" w:uri="http://schemas.microsoft.com/office/word" w:val="1"/><w:compatSetting w:name="useWord2013TrackBottomHyphenation" w:uri="http://schemas.microsoft.com/office/word" w:val="1"/></w:compat><w:themeFontLang w:val="cs-CZ"/><w:decimalSymbol w:val=","/><w:listSeparator w:val=";"/></w:settings>""";

    // Prázdné řádky vzoru — každý s formátem, jaký má ve vzoru na svém místě.
    private const string PrazdnyPredSekci1 = """<w:p><w:pPr><w:pStyle w:val="Default"/><w:rPr><w:sz w:val="23"/><w:szCs w:val="23"/></w:rPr></w:pPr></w:p>""";
    private const string PrazdnyPredTabulkou = """<w:p><w:pPr><w:ind w:left="-284"/></w:pPr></w:p>""";
    private const string PrazdnyTucnyDefault = """<w:p><w:pPr><w:pStyle w:val="Default"/><w:rPr><w:b/></w:rPr></w:pPr></w:p>""";
    private const string PrazdnyDefault = """<w:p><w:pPr><w:pStyle w:val="Default"/></w:pPr></w:p>""";
    private const string PrazdnyTucny = """<w:p><w:pPr><w:rPr><w:b/></w:rPr></w:pPr></w:p>""";
    private const string PrazdnyPodCislemUkolu = """<w:p><w:pPr><w:rPr><w:u w:val="single"/></w:rPr></w:pPr></w:p>""";
    private const string PrazdnyVTextuPozadavku = """<w:p><w:pPr><w:ind w:left="426"/><w:jc w:val="both"/></w:pPr></w:p>""";
    private const string PrazdnySekce2 = """<w:p><w:pPr><w:jc w:val="both"/><w:rPr><w:bCs/><w:u w:val="single"/></w:rPr></w:pPr></w:p>""";

    /// <summary>Hlavička dokumentu až po prázdný řádek pod nadpisem výzvy.</summary>
    public static IEnumerable<Paragraph> HlavickaUradu(string kodVyzvy, string informacniSystem)
    {
        var odstavce = HlavickaXml.Select(Prvek<Paragraph>).ToList();
        odstavce[IndexNadpisu].Descendants<Text>().Single().Text =
            $"Výzva k poskytnutí plnění č. {kodVyzvy} pro {informacniSystem}";
        return odstavce;
    }

    /// <summary>
    /// Úvod od „Veřejný zadavatel“ po prázdný řádek před sekcí 1. Název zákona opravený proti vzoru
    /// („zakázkách“ → „zakázek“, spec B9). Pod větou s VZ je o řádek víc místa než ve vzoru —
    /// uživatel 2026-10-06 chtěl před bodem 1 větší mezeru.
    /// </summary>
    public static IEnumerable<Paragraph> Uvod(string cisloRamcoveSmlouvy, string poradoveCislo)
    {
        yield return Prvek<Paragraph>($"""<w:p><w:pPr><w:jc w:val="both"/><w:rPr><w:b/></w:rPr></w:pPr><w:r><w:t xml:space="preserve">Veřejný zadavatel Česká republika – Ministerstvo obrany, se sídlem Tychonova 1, Praha 6, zastoupena ředitelem odboru vyzbrojování pozemních sil a KIS Sekce vyzbrojování a akvizic MO Ing. Petrem ZÁBORCEM, se sídlem na adrese náměstí Svobody 471/4, 160 01 Praha 6 (dále jen „nabyvatel“), Vás vyzývá podle ustanovení § 134 zákona č. 134/2016 Sb., o zadávání veřejných zakázek, ve znění pozdějších předpisů, v souladu s čl. IV. rámcové dohody číslo {Esc(cisloRamcoveSmlouvy)} (dále jen „rámcová dohoda“) a v souladu s podmínkami v ní uvedenými</w:t></w:r></w:p>""");
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:jc w:val="center"/></w:pPr><w:r><w:rPr><w:b/></w:rPr><w:t xml:space="preserve">k poskytnutí plnění</w:t></w:r></w:p>""");
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:ind w:firstLine="708"/><w:jc w:val="center"/></w:pPr></w:p>""");
        yield return Prvek<Paragraph>($"""<w:p><w:pPr><w:spacing w:after="276"/><w:jc w:val="both"/></w:pPr><w:r><w:t xml:space="preserve">veřejné zakázky „</w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>Technické zhodnocení APV a DZ</w:t></w:r><w:r><w:t xml:space="preserve">“ pořadové číslo </w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>{Esc(poradoveCislo)}</w:t></w:r><w:r><w:t xml:space="preserve"> (dále jen „Výzva“) na zadání dílčí veřejné zakázky.</w:t></w:r></w:p>""");
        yield return Prvek<Paragraph>(PrazdnyPredSekci1);
    }

    /// <summary>Nadpis sekce 1.–7. Sekce 2–4 začínají na nové stránce vlastností nadpisu (spec B8).</summary>
    public static Paragraph NadpisSekce(string text, bool novaStrana = false)
        => Prvek<Paragraph>($"""<w:p><w:pPr><w:pStyle w:val="Default"/><w:keepNext/>{(novaStrana ? "<w:pageBreakBefore/>" : string.Empty)}<w:numPr><w:ilvl w:val="0"/><w:numId w:val="{NumSekce}"/></w:numPr><w:rPr><w:b/></w:rPr></w:pPr><w:r><w:rPr><w:b/></w:rPr><w:t xml:space="preserve">{Esc(text)}</w:t></w:r></w:p>""");

    public static Paragraph PrazdnyPredTabulkouPredmetu() => Prvek<Paragraph>(PrazdnyPredTabulkou);

    /// <summary>Sekce 1 pod tabulkou: odstavec o návrzích a „Stručné popisy požadavků:“ s prázdnými řádky vzoru.</summary>
    public static IEnumerable<Paragraph> PodTabulkouPredmetu()
    {
        yield return Prvek<Paragraph>(PrazdnyTucnyDefault);
        yield return Prvek<Paragraph>($"""<w:p><w:pPr><w:jc w:val="both"/></w:pPr><w:r><w:t xml:space="preserve">{PodrobneNavrhy}</w:t></w:r></w:p>""");
        yield return Prvek<Paragraph>(PrazdnyDefault);
        yield return Prvek<Paragraph>(PrazdnyTucnyDefault);
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:pStyle w:val="Default"/><w:keepNext/><w:rPr><w:b/></w:rPr></w:pPr><w:r><w:rPr><w:b/></w:rPr><w:t>Stručné popisy požadavků:</w:t></w:r></w:p>""");
        yield return Prvek<Paragraph>(PrazdnyTucny);
    }

    /// <summary>Nadpis požadavku — v sekci 1 s odsazením vzoru, v sekci 2 s odsazením z číslování.</summary>
    public static Paragraph NadpisPozadavku(string? nazev, bool sekce1)
        => Prvek<Paragraph>($"""<w:p><w:pPr><w:pStyle w:val="Odstavecseseznamem"/><w:keepNext/><w:numPr><w:ilvl w:val="0"/><w:numId w:val="{(sekce1 ? NumPozadavkySekce1 : NumPozadavkySekce2)}"/></w:numPr>{(sekce1 ? OdsazeniNadpisuSekce1 : string.Empty)}<w:rPr>{Tnr}<w:b/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr></w:pPr><w:r><w:rPr>{Tnr}<w:b/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr><w:t xml:space="preserve">{Esc(nazev ?? string.Empty)}</w:t></w:r></w:p>""");

    /// <summary>„Číslo úkolu VP EIS: …“ kurzívou; v sekci 1 s prázdným řádkem pod sebou.</summary>
    public static IEnumerable<Paragraph> CisloUkolu(string text, bool sekce1)
    {
        yield return Prvek<Paragraph>($"""<w:p><w:pPr><w:keepNext/><w:ind w:left="{(sekce1 ? 426 : 360)}"/><w:rPr><w:i/></w:rPr></w:pPr><w:r><w:rPr><w:i/></w:rPr><w:t xml:space="preserve">{Esc(text)}</w:t></w:r></w:p>""");
        if (sekce1)
        {
            yield return Prvek<Paragraph>(PrazdnyPodCislemUkolu);
        }
    }

    public static Paragraph PrazdnyRadekTextuPozadavku() => Prvek<Paragraph>(PrazdnyVTextuPozadavku);

    public static Paragraph BlizsiPodrobnosti(string cisloHtl)
        => Prvek<Paragraph>($"""<w:p><w:pPr><w:spacing w:after="120"/><w:ind w:left="426"/></w:pPr><w:r><w:t xml:space="preserve">Bližší podrobnosti jsou uvedeny v PNF {Esc(cisloHtl)}.</w:t></w:r></w:p>""");

    public static Paragraph PrazdnyMeziPozadavkySekce1() => Prvek<Paragraph>(PrazdnyTucny);

    public static Paragraph PrazdnyPodNadpisemSekce2() => Prvek<Paragraph>("<w:p/>");

    public static Paragraph PrazdnyRadekSekce2() => Prvek<Paragraph>(PrazdnySekce2);

    /// <summary>Štítek tabulky v sekci 2 („Individuální úpravy“, „Licenční rozšíření“).</summary>
    public static Paragraph StitekKalkulace(string text)
        => Prvek<Paragraph>($"""<w:p><w:pPr><w:keepNext/><w:spacing w:before="120"/><w:jc w:val="both"/><w:rPr><w:sz w:val="20"/></w:rPr></w:pPr><w:r><w:rPr><w:sz w:val="20"/></w:rPr><w:t xml:space="preserve">{Esc(text)}</w:t></w:r></w:p>""");

    /// <summary>Sekce 3: prázdný řádek pod nadpisem a štítek „Individuální úpravy:“.</summary>
    public static IEnumerable<Paragraph> NadRekapitulaciUprav()
    {
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:rPr><w:b/><w:u w:val="single"/></w:rPr></w:pPr></w:p>""");
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:keepNext/></w:pPr><w:r><w:t>Individuální úpravy:</w:t></w:r></w:p>""");
    }

    /// <summary>Sekce 3: prázdný řádek a štítek „Licenční rozšíření:“.</summary>
    public static IEnumerable<Paragraph> NadRekapitulaciLicenci()
    {
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:rPr><w:b/><w:bCs/></w:rPr></w:pPr></w:p>""");
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:keepNext/></w:pPr><w:r><w:t xml:space="preserve">Licenční rozšíření:</w:t></w:r></w:p>""");
    }

    /// <summary>Sekce 3: prázdný řádek mezi rekapitulací licencí a souhrnem (sousední tabulky by se slily).</summary>
    public static Paragraph PrazdnyPredSouhrnem() => Prvek<Paragraph>("""<w:p><w:pPr><w:rPr><w:bCs/></w:rPr></w:pPr></w:p>""");

    /// <summary>
    /// Sekce 4–7 doslova ze vzoru. Termín plnění zůstává prázdný (komentář autora vzoru „Nechat
    /// volné“); místo plnění je z projektu — tučně jako ve vzoru je jen část po první dvojtečku.
    /// </summary>
    public static IEnumerable<Paragraph> Zaver(string mistoPlneni)
    {
        yield return NadpisSekce("Identifikační údaje nabyvatele", novaStrana: true);
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:spacing w:before="120"/><w:ind w:firstLine="357"/></w:pPr><w:r><w:t xml:space="preserve">Česká republika – Ministerstvo obrany</w:t></w:r></w:p>""");
        foreach (var radek in new[]
                 {
                     "Tychonova 1", "160 00 Praha 6", "IČO: 60162694, DIČ: CZ60162694", "v zastoupení",
                     "Sekce vyzbrojování a akvizic MO  ",
                     "odbor vyzbrojování pozemních sil a komunikačních a informačních systémů", "náměstí Svobody 471/4",
                 })
        {
            yield return Prvek<Paragraph>($"""<w:p><w:pPr><w:ind w:firstLine="360"/></w:pPr><w:r><w:t xml:space="preserve">{radek}</w:t></w:r></w:p>""");
        }

        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:ind w:left="357"/></w:pPr><w:r><w:t>160 01 Praha 6</w:t></w:r></w:p>""");
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:ind w:left="357"/><w:rPr><w:b/></w:rPr></w:pPr></w:p>""");

        yield return NadpisSekce("Termín a místo plnění");
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:spacing w:before="120"/><w:ind w:left="357"/><w:rPr><w:b/></w:rPr></w:pPr><w:r><w:t xml:space="preserve">Termín pro splnění dílčí veřejné zakázky do: </w:t></w:r></w:p>""");
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:spacing w:before="120"/><w:ind w:left="357"/></w:pPr><w:r><w:t>Místem plnění je:</w:t></w:r></w:p>""");
        var dvojtecka = mistoPlneni.IndexOf(':');
        var tucne = dvojtecka >= 0 ? mistoPlneni[..(dvojtecka + 1)] + " " : mistoPlneni;
        var zbytek = dvojtecka >= 0 ? mistoPlneni[(dvojtecka + 1)..].TrimStart() : string.Empty;
        yield return Prvek<Paragraph>($"""<w:p><w:pPr><w:suppressAutoHyphens/><w:spacing w:before="120"/><w:ind w:left="360" w:firstLine="348"/><w:jc w:val="both"/></w:pPr><w:r><w:rPr><w:b/><w:szCs w:val="20"/></w:rPr><w:t xml:space="preserve">{Esc(tucne)}</w:t></w:r><w:r><w:t xml:space="preserve">{Esc(zbytek)}</w:t></w:r></w:p>""");
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:suppressAutoHyphens/><w:spacing w:before="120"/><w:ind w:left="360" w:firstLine="348"/><w:jc w:val="both"/></w:pPr></w:p>""");

        yield return NadpisSekce("Lhůta pro písemné potvrzení Výzvy");
        yield return Prvek<Paragraph>($"""<w:p><w:pPr><w:pStyle w:val="Odstavecseseznamem"/><w:spacing w:before="120"/><w:ind w:left="360"/><w:rPr><w:bCs/></w:rPr></w:pPr><w:r><w:rPr>{Tnr}<w:b/><w:bCs/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr><w:t>Dodavatel</w:t></w:r><w:r><w:rPr>{Tnr}<w:bCs/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr><w:t xml:space="preserve"> dle čl. IV odst. 1 </w:t></w:r><w:r><w:rPr>{Tnr}<w:b/><w:bCs/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr><w:t>rámcové smlouvy</w:t></w:r><w:r><w:rPr>{Tnr}<w:bCs/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr><w:t xml:space="preserve"> potvrdí tuto </w:t></w:r><w:r><w:rPr>{Tnr}<w:b/><w:bCs/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr><w:t>Výzvu</w:t></w:r><w:r><w:rPr>{Tnr}<w:bCs/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr><w:t xml:space="preserve"> do 5 dnů od jejího doručení.</w:t></w:r></w:p>""");

        yield return NadpisSekce("Datum a místo potvrzení výzvy dodavatelem");
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:spacing w:before="120"/><w:ind w:left="357"/><w:rPr><w:b/><w:bCs/></w:rPr></w:pPr></w:p>""");
        // Podpisy: text písmem stylu Normální, sloupce tabulátory, šest prázdných řádků na podpis.
        // Obě jména ponechal uživatel 2026-09-10 — jsou ve vzoru a s výzvou se nemění.
        yield return Podpis("""<w:r><w:rPr><w:rStyle w:val="normaltextrun"/></w:rPr><w:t xml:space="preserve">Za </w:t></w:r><w:r><w:rPr><w:rStyle w:val="normaltextrun"/><w:b/><w:bCs/></w:rPr><w:t>nabyvatele</w:t></w:r><w:r><w:rPr><w:rStyle w:val="normaltextrun"/></w:rPr><w:t>:</w:t></w:r>""" + Tabulatory(5) + """<w:r><w:rPr><w:rStyle w:val="normaltextrun"/></w:rPr><w:t xml:space="preserve">Za </w:t></w:r><w:r><w:rPr><w:rStyle w:val="normaltextrun"/><w:b/><w:bCs/></w:rPr><w:t>dodavatele</w:t></w:r><w:r><w:rPr><w:rStyle w:val="normaltextrun"/></w:rPr><w:t>:</w:t></w:r>""");
        for (var i = 0; i < 6; i++)
        {
            yield return Podpis(string.Empty);
        }

        yield return Podpis(TextPodpisu("………………………") + Tabulatory(4) + TextPodpisu("………………………"));
        yield return Podpis(TextPodpisu("Ing. Petr ZÁBOREC") + Tabulatory(5) + TextPodpisu("Ing. Břetislav MOC"));
        yield return Podpis(TextPodpisu("ředitel") + Tabulatory(7) + TextPodpisu("předseda správní rady"));
        yield return Prvek<Paragraph>("""<w:p><w:pPr><w:tabs><w:tab w:val="center" w:pos="6804"/></w:tabs><w:jc w:val="both"/></w:pPr></w:p>""");
    }

    private static string TextPodpisu(string text)
        => $"""<w:r><w:rPr><w:rStyle w:val="normaltextrun"/></w:rPr><w:t>{Esc(text)}</w:t></w:r>""";

    private static string Tabulatory(int pocet)
        => string.Concat(Enumerable.Repeat("""<w:r><w:rPr><w:rStyle w:val="tabchar"/><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/></w:rPr><w:tab/></w:r>""", pocet));

    /// <summary>Odstavec podpisové části vzoru; běh „eop“ s mezerou drží výšku řádku i u prázdného.</summary>
    private static Paragraph Podpis(string behy)
        => Prvek<Paragraph>($"""<w:p><w:pPr><w:pStyle w:val="paragraph"/><w:spacing w:before="0" w:beforeAutospacing="0" w:after="0" w:afterAutospacing="0"/><w:jc w:val="both"/><w:textAlignment w:val="baseline"/><w:rPr><w:rFonts w:ascii="Segoe UI" w:hAnsi="Segoe UI" w:cs="Segoe UI"/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr></w:pPr>{behy}<w:r><w:rPr><w:rStyle w:val="eop"/></w:rPr><w:t xml:space="preserve"> </w:t></w:r></w:p>""");

    /// <summary>Motiv vzoru jako data části theme (kořen je v jmenném prostoru DrawingML, ne w:).</summary>
    public static Stream Motiv() => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(MotivXml));

    public static Header Zahlavi() => Prvek<Header>(ZahlaviXml);

    public static Footer Zapati() => Prvek<Footer>(ZapatiXml);

    public static Styles Styly() => Prvek<Styles>(StylyXml.ReplaceLineEndings(string.Empty));

    // Plně kvalifikováno: „Settings“ je v aplikaci i jmenný prostor.
    public static DocumentFormat.OpenXml.Wordprocessing.Settings Nastaveni()
        => Prvek<DocumentFormat.OpenXml.Wordprocessing.Settings>(NastaveniXml);

    /// <summary>Číslování vzoru: abstraktní definice a instance sekcí a obou řad požadavků.</summary>
    public static Numbering Cislovani()
    {
        var numbering = new Numbering(AbstraktniCislovaniXml.Select(Prvek<AbstractNum>));
        numbering.Append(
            new NumberingInstance(new AbstractNumId { Val = 18 }) { NumberID = NumSekce },
            new NumberingInstance(new AbstractNumId { Val = 4 }) { NumberID = NumPozadavkySekce1 },
            new NumberingInstance(new AbstractNumId { Val = 10 }) { NumberID = NumPozadavkySekce2 });
        return numbering;
    }

    private static string Esc(string text) => SecurityElement.Escape(text) ?? string.Empty;

    /// <summary>Prvek z XML fragmentu vzoru; kořenu doplní deklaraci jmenného prostoru w:.</summary>
    private static T Prvek<T>(string xml) where T : OpenXmlElement
    {
        var konecZnacky = xml.IndexOfAny(new[] { '>', ' ', '/' });
        var prvek = (T)Activator.CreateInstance(typeof(T), $"{xml[..konecZnacky]} xmlns:w=\"{W}\"{xml[konecZnacky..]}")!;

        // SDK při čtení uloží odsazení w:left jako w:start (zápis Office 2010). Vzor má w:left,
        // které zná i Word 2007 a validátor — vrátit zpět.
        foreach (var odsazeni in prvek.Descendants<Indentation>().Concat(prvek is Indentation i ? new[] { i } : Array.Empty<Indentation>()))
        {
            if (odsazeni.Start is not null && odsazeni.Left is null)
            {
                odsazeni.Left = odsazeni.Start.Value;
                odsazeni.Start = null;
            }
        }

        return prvek;
    }
}
