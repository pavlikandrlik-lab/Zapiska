using DocumentFormat.OpenXml.Wordprocessing;

namespace PmTracker.Web.Services.Export;

/// <summary>
/// Části Wordu výzvy převzaté 1:1 z XML vzoru <c>2027xxxx_N_8201_Vyzva_c_x_2027_EIS.docx</c>
/// (word/document.xml, header1.xml, styles.xml, settings.xml; uživatel 2026-10-07: „zkopírovat 1:1“).
/// Proti vzoru jsou odstraněné jen identifikátory revizí (rsid), značky kontroly pravopisu,
/// komentáře autora vzoru a mezery v prázdném řádku; běhy se stejným formátem jsou sloučené.
/// Číslo jednací v záhlaví a údaje konkrétní výzvy zůstávají prázdné, resp. doplní je export.
/// Při změně vzoru XML znovu zkopírovat, ne dolaďovat formát ručně.
/// </summary>
internal static class VyzvaWordVzor
{
    /// <summary>Styl Zápatí vzoru (pro číslo stránky).</summary>
    public const string StylZapati = "Zpat";

    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

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

    // header1.xml — číslo jednací vzoru (818688/2026-8201) prázdné k doplnění (spec B2).
    private const string ZahlaviXml =
        """<w:hdr><w:p><w:pPr><w:pStyle w:val="Zhlav"/><w:jc w:val="right"/></w:pPr><w:r><w:t xml:space="preserve">Příloha č.1 k Čj. MO </w:t></w:r></w:p></w:hdr>""";

    // styles.xml — výchozí formát a styly, na kterých stojí hlavička, záhlaví a zápatí.
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
        </w:styles>
        """;

    // settings.xml — položky ovlivňující sazbu. Vynechané: zoom, revize (rsids), poznámky pod
    // čarou (dokument je nemá), vkládání písem, odstranění osobních údajů, ID dokumentu.
    private const string NastaveniXml =
        """<w:settings><w:defaultTabStop w:val="708"/><w:hyphenationZone w:val="425"/><w:noPunctuationKerning/><w:characterSpacingControl w:val="doNotCompress"/><w:compat><w:compatSetting w:name="compatibilityMode" w:uri="http://schemas.microsoft.com/office/word" w:val="15"/><w:compatSetting w:name="overrideTableStyleFontSizeAndJustification" w:uri="http://schemas.microsoft.com/office/word" w:val="1"/><w:compatSetting w:name="enableOpenTypeFeatures" w:uri="http://schemas.microsoft.com/office/word" w:val="1"/><w:compatSetting w:name="doNotFlipMirrorIndents" w:uri="http://schemas.microsoft.com/office/word" w:val="1"/><w:compatSetting w:name="differentiateMultirowTableHeaders" w:uri="http://schemas.microsoft.com/office/word" w:val="1"/><w:compatSetting w:name="useWord2013TrackBottomHyphenation" w:uri="http://schemas.microsoft.com/office/word" w:val="1"/></w:compat><w:themeFontLang w:val="cs-CZ"/><w:decimalSymbol w:val=","/><w:listSeparator w:val=";"/></w:settings>""";

    /// <summary>Hlavička dokumentu až po prázdný řádek pod nadpisem výzvy.</summary>
    public static IEnumerable<Paragraph> HlavickaUradu(string kodVyzvy, string informacniSystem)
    {
        var odstavce = HlavickaXml.Select(xml => new Paragraph(SNamespace(xml))).ToList();
        odstavce[IndexNadpisu].Descendants<Text>().Single().Text =
            $"Výzva k poskytnutí plnění č. {kodVyzvy} pro {informacniSystem}";
        return odstavce;
    }

    public static Header Zahlavi() => new(SNamespace(ZahlaviXml));

    public static Styles Styly() => new(SNamespace(StylyXml.ReplaceLineEndings(string.Empty)));

    // Plně kvalifikováno: „Settings“ je v aplikaci i jmenný prostor.
    public static DocumentFormat.OpenXml.Wordprocessing.Settings Nastaveni() => new(SNamespace(NastaveniXml));

    /// <summary>Kořenu fragmentu doplní deklaraci jmenného prostoru w:, kterou XML vzoru nese v kořeni dokumentu.</summary>
    private static string SNamespace(string xml)
    {
        var konecZnacky = xml.IndexOfAny(new[] { '>', ' ' });
        return $"{xml[..konecZnacky]} xmlns:w=\"{W}\"{xml[konecZnacky..]}";
    }
}
