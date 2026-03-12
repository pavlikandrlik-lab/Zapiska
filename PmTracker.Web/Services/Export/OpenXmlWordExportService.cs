using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Export;

public sealed partial class OpenXmlWordExportService : IWordExportService
{
    private const int BaseFontHalfPoints = 20; // 10 pt
    private const int RecordTitleHalfPoints = 24; // 12 pt
    private readonly IRichTextContentService _richTextContentService;

    public OpenXmlWordExportService(IRichTextContentService richTextContentService)
    {
        _richTextContentService = richTextContentService;
    }

    public byte[] BuildDocument(PdfExportTemplateViewModel model)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body ?? throw new InvalidOperationException("Word body nebyl inicializován.");

            AppendHeaderTable(body, model);
            AppendRecordsTable(body, mainPart, model.Zaznamy);
            AppendSectionProperties(body);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static string BuildDocumentTitle(PdfExportTemplateViewModel model)
    {
        var variant = (model.ExportVariant ?? "project_all").Trim().ToLowerInvariant();
        return variant switch
        {
            "meeting" => $"Zápis z jednání projektu {model.ProjektNazev} číslo {model.JednaniCislo}",
            "task_single" => $"Zápis úkolu projektu {model.ProjektNazev}",
            _ => $"Souhrnný zápis projektu {model.ProjektNazev}"
        };
    }

    private static void AppendHeaderTable(Body body, PdfExportTemplateViewModel model)
    {
        var variant = (model.ExportVariant ?? "project_all").Trim().ToLowerInvariant();
        var isMeeting = string.Equals(variant, "meeting", StringComparison.OrdinalIgnoreCase);
        var table = CreateHeaderTableSkeleton();
        table.Append(CreateHeaderTitleRow(BuildDocumentTitle(model)));
        table.Append(CreateHeaderKeyValueRow("Projekt", $"{model.ProjektNazev} ({model.ProjektZkratka})"));

        if (isMeeting)
        {
            table.Append(CreateHeaderKeyValueRow("Jednání", $"č. {model.JednaniCislo}"));
            table.Append(CreateHeaderKeyValueRow("Datum", model.JednaniDatum?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "-"));
            table.Append(CreateHeaderKeyValueRow("Stav", model.JednaniStav));
            table.Append(CreateHeaderKeyValueRow("Místo", string.IsNullOrWhiteSpace(model.JednaniMisto) ? "-" : model.JednaniMisto));
        }
        else
        {
            table.Append(CreateHeaderKeyValueRow("Typ výstupu", variant == "task_single" ? "Jeden úkol" : "Kompletní projekt"));
        }

        table.Append(CreateHeaderKeyValueRow("Generoval", model.Vytvoril));
        table.Append(CreateHeaderKeyValueRow("Vytvořeno", model.VytvorenoDne.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)));

        if (!isMeeting && model.ProjektoveRole.Count > 0)
        {
            var roleLines = model.ProjektoveRole
                .Select(member =>
                {
                    var subsystemSuffix = string.IsNullOrWhiteSpace(member.Subsystem)
                        ? string.Empty
                        : $" ({member.Subsystem})";
                    return $"{member.Osoba} — {member.TypRole}: {member.Role}{subsystemSuffix}";
                })
                .ToList();
            table.Append(CreateHeaderMultilineRow("Projektové role", roleLines));
        }

        if (model.Dochazka.Count > 0)
        {
            foreach (var group in model.Dochazka)
            {
                var value = group.Osoby.Count == 0 ? "-" : string.Join(", ", group.Osoby);
                table.Append(CreateHeaderKeyValueRow(group.Stav, value, true));
            }
        }

        body.Append(table);
        body.Append(CreateSpacerParagraph(80));
    }

    private static Table CreateHeaderTableSkeleton()
    {
        var table = new Table();
        table.Append(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new BottomBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new LeftBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new RightBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new InsideHorizontalBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new InsideVerticalBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U }),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableLayout { Type = TableLayoutValues.Fixed }));

        table.Append(new TableGrid(
            new GridColumn { Width = "2600" },
            new GridColumn { Width = "7400" }));

        return table;
    }

    private static void AppendSectionProperties(Body body)
    {
        if (body.Elements<SectionProperties>().Any())
        {
            return;
        }

        body.Append(new SectionProperties(
            new PageSize
            {
                Width = 11906U,
                Height = 16838U,
                Orient = PageOrientationValues.Portrait
            },
            new PageMargin
            {
                Top = 720,
                Right = 720U,
                Bottom = 720,
                Left = 720U,
                Header = 420U,
                Footer = 420U,
                Gutter = 0U
            }));
    }

    private static TableRow CreateHeaderTitleRow(string title)
    {
        var row = new TableRow();
        var cellProperties = new TableCellProperties(
            new GridSpan { Val = 2 },
            new Shading { Val = ShadingPatternValues.Clear, Fill = "D7ECFB", Color = "auto" });

        var cell = new TableCell(cellProperties);
        cell.Append(CreateParagraph(
            title,
            bold: true,
            sizeHalfPoints: 30,
            justification: JustificationValues.Center,
            before: 80,
            after: 80));

        row.Append(cell);
        return row;
    }

    private static TableRow CreateHeaderKeyValueRow(string label, string value, bool emphasizeLabel = true)
    {
        var row = new TableRow();
        row.Append(CreateCell(label, bold: emphasizeLabel));
        row.Append(CreateCell(value, bold: false));
        return row;
    }

    private static TableRow CreateHeaderMultilineRow(string label, IReadOnlyList<string> lines)
    {
        var row = new TableRow();
        row.Append(CreateCell(label, bold: true));

        var valueCell = new TableCell(new TableCellProperties());
        if (lines.Count == 0)
        {
            valueCell.Append(CreateParagraph("-", bold: false, sizeHalfPoints: BaseFontHalfPoints, before: 30, after: 30));
        }
        else
        {
            foreach (var line in lines)
            {
                valueCell.Append(CreateParagraph(line, bold: false, sizeHalfPoints: BaseFontHalfPoints, before: 20, after: 20));
            }
        }

        row.Append(valueCell);
        return row;
    }

    private void AppendRecordsTable(Body body, MainDocumentPart mainPart, IReadOnlyList<PdfExportRecordViewModel> records)
    {
        var table = CreateRecordsTableSkeleton();
        table.Append(CreateHeaderRow());

        if (records.Count == 0)
        {
            table.Append(CreateSingleCellRow("Žádná data k tisku.", gridSpan: 3, fillColor: null, italic: true));
            body.Append(table);
            return;
        }

        var subsystemGroups = records
            .GroupBy(x => x.Subsystem)
            .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var subsystemGroup in subsystemGroups)
        {
            table.Append(CreateSingleCellRow($"Subsystém: {subsystemGroup.Key}", gridSpan: 3, fillColor: "EAF2FA"));

            var orderedRecords = subsystemGroup
                .OrderBy(record => CategoryOrder(record.Kategorie))
                .ThenBy(record => record.Kategorie, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(record => record.CisloViditelneA)
                .ThenBy(record => record.CisloViditelneB)
                .ThenBy(record => record.CisloZaznamu)
                .ToList();

            foreach (var record in orderedRecords)
            {
                table.Append(CreateRecordRow(mainPart, record));
            }
        }

        body.Append(table);
    }

    private static Table CreateRecordsTableSkeleton()
    {
        var table = new Table();
        table.Append(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new BottomBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new LeftBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new RightBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new InsideHorizontalBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new InsideVerticalBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U }),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableLayout { Type = TableLayoutValues.Fixed }));

        table.Append(new TableGrid(
            new GridColumn { Width = "8100" },
            new GridColumn { Width = "2100" },
            new GridColumn { Width = "1600" }));

        return table;
    }

    private static TableRow CreateHeaderRow()
    {
        return new TableRow(
            CreateCell("Záznamy a vyjádření", bold: true, fillColor: "F3F4F6"),
            CreateCell("Osoby", bold: true, fillColor: "F3F4F6"),
            CreateCell("Termíny", bold: true, fillColor: "F3F4F6"));
    }

    private static TableRow CreateSingleCellRow(string text, int gridSpan, string? fillColor = null, bool italic = false)
    {
        var cellProps = new TableCellProperties(new GridSpan { Val = gridSpan });
        if (!string.IsNullOrWhiteSpace(fillColor))
        {
            cellProps.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = fillColor, Color = "auto" });
        }

        var cell = new TableCell(cellProps);
        cell.Append(CreateParagraph(text, bold: !italic, italic: italic, sizeHalfPoints: italic ? 19 : 22, before: 40, after: 40));
        return new TableRow(cell);
    }

    private TableRow CreateRecordRow(MainDocumentPart mainPart, PdfExportRecordViewModel record)
    {
        var commentsCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "8100" }));
        var peopleCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2100" }));
        var deadlinesCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "1600" }));

        AppendRecordCommentsCell(commentsCell, mainPart, record);
        AppendRecordPeopleCell(peopleCell, record);
        AppendRecordDeadlinesCell(deadlinesCell, record);

        return new TableRow(commentsCell, peopleCell, deadlinesCell);
    }

    private void AppendRecordCommentsCell(TableCell cell, MainDocumentPart mainPart, PdfExportRecordViewModel record)
    {
        var code = string.IsNullOrWhiteSpace(record.TypUkoluKod)
            ? (string.IsNullOrWhiteSpace(record.KategorieKod) ? "-" : record.KategorieKod)
            : record.TypUkoluKod;
        var isPaused = (record.Stav ?? string.Empty).Contains("pozastav", StringComparison.CurrentCultureIgnoreCase);
        var pausedFill = isPaused ? "FDF4E8" : null;

        cell.Append(CreateParagraph(
            $"{code}{record.CisloViditelne} - {record.Nazev}",
            bold: true,
            sizeHalfPoints: RecordTitleHalfPoints,
            before: 40,
            after: 40,
            shadingHex: pausedFill));

        cell.Append(CreateRichParagraph(
                new[]
                {
                    new TextSegment("Stav: ", Bold: true),
                    new TextSegment(string.IsNullOrWhiteSpace(record.Stav) ? "-" : record.Stav),
                    new TextSegment(" | "),
                    new TextSegment("Typ úkolu: ", Bold: true),
                    new TextSegment(string.IsNullOrWhiteSpace(record.TypUkolu) ? "-" : record.TypUkolu!)
                },
            sizeHalfPoints: BaseFontHalfPoints,
            before: 0,
            after: 25));

        if (!string.IsNullOrWhiteSpace(record.Cil))
        {
            cell.Append(CreateRichParagraph(
                new[]
                {
                    new TextSegment("Cíl: ", Bold: true, Italic: true),
                    new TextSegment(record.Cil, Italic: true, PreserveLineBreaks: true)
                },
                sizeHalfPoints: BaseFontHalfPoints,
                before: 0,
                after: 25));
        }

        if (!string.IsNullOrWhiteSpace(record.Popis))
        {
            var safeDescriptionHtml = _richTextContentService.ToSafeHtml(record.Popis);
            if (!string.IsNullOrWhiteSpace(safeDescriptionHtml))
            {
                AppendHtmlParagraphs(
                    cell,
                    mainPart,
                    safeDescriptionHtml,
                    new TextSegment("Popis: ", Bold: true),
                    beforeFirst: 0,
                    afterLast: 25,
                    shadingHex: null);
            }
        }

        if (record.ExterniVazby.Count > 0)
        {
            cell.Append(CreateRichParagraph(
                new[]
                {
                    new TextSegment("Externí vazby: ", Bold: true),
                    new TextSegment(string.Join("; ", record.ExterniVazby))
                },
                sizeHalfPoints: BaseFontHalfPoints,
                before: 0,
                after: 25));
        }

        foreach (var comment in record.Vyjadreni)
        {
            var meetingRef = comment.JednaniCislo.HasValue
                ? $"jednání č. {comment.JednaniCislo}{(comment.JednaniDatum.HasValue ? $" ({comment.JednaniDatum.Value:dd.MM.yyyy})" : string.Empty)}"
                : "jednání";
            var titleText = $"{meetingRef} | {comment.Autor} | {comment.Datum:dd.MM.yyyy}";
            var commentTextColor = NormalizeHexColor(comment.HighlightColor);
            cell.Append(CreateParagraph(
                titleText,
                bold: true,
                sizeHalfPoints: BaseFontHalfPoints,
                before: 20,
                after: 0,
                colorHex: commentTextColor));
            var safeCommentHtml = _richTextContentService.ToSafeHtml(comment.Text);
            if (!string.IsNullOrWhiteSpace(safeCommentHtml))
            {
                AppendHtmlParagraphs(
                    cell,
                    mainPart,
                    safeCommentHtml,
                    prefixSegment: null,
                    beforeFirst: 0,
                    afterLast: 20,
                    shadingHex: null,
                    textColorHex: commentTextColor);
            }
        }
    }

    private static void AppendRecordPeopleCell(TableCell cell, PdfExportRecordViewModel record)
    {
        cell.Append(CreateRichParagraph(
            new[]
            {
                new TextSegment("Vlastník: ", Bold: true),
                new TextSegment(record.Vlastnik)
            },
            sizeHalfPoints: BaseFontHalfPoints,
            before: 40,
            after: 30));

        if (record.Spoluprace.Count == 0)
        {
            return;
        }

        cell.Append(CreateParagraph("Spolupráce:", bold: true, sizeHalfPoints: BaseFontHalfPoints, before: 20, after: 20));
        foreach (var person in record.Spoluprace)
        {
            cell.Append(CreateParagraph(person, sizeHalfPoints: BaseFontHalfPoints, before: 0, after: 20));
        }
    }

    private static void AppendRecordDeadlinesCell(TableCell cell, PdfExportRecordViewModel record)
    {
        cell.Append(CreateRichParagraph(
            new[]
            {
                new TextSegment("Založeno: ", Bold: true),
                new TextSegment(record.DatumZalozeni.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture))
            },
            sizeHalfPoints: BaseFontHalfPoints,
            before: 40,
            after: 20));

        cell.Append(CreateRichParagraph(
            new[]
            {
                new TextSegment("Aktuální termín: ", Bold: true),
                new TextSegment(record.Termin.HasValue ? record.Termin.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : "-")
            },
            sizeHalfPoints: BaseFontHalfPoints,
            before: 0,
            after: 20));

        if (record.HistorieTerminu.Count == 0)
        {
            return;
        }

        cell.Append(CreateParagraph("Historie termínů:", bold: true, sizeHalfPoints: BaseFontHalfPoints, before: 20, after: 20));
        foreach (var date in record.HistorieTerminu.OrderByDescending(x => x))
        {
            cell.Append(CreateParagraph(date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), strike: true, sizeHalfPoints: BaseFontHalfPoints, before: 0, after: 20));
        }
    }

    private static TableCell CreateCell(string text, bool bold = false, string? fillColor = null)
    {
        var properties = new TableCellProperties();
        if (!string.IsNullOrWhiteSpace(fillColor))
        {
            properties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = fillColor, Color = "auto" });
        }

        var cell = new TableCell(properties);
        cell.Append(CreateParagraph(text, bold: bold, sizeHalfPoints: BaseFontHalfPoints, before: 30, after: 30));
        return cell;
    }

    private static Paragraph CreateRichParagraph(
        IReadOnlyList<TextSegment> segments,
        int sizeHalfPoints = BaseFontHalfPoints,
        JustificationValues? justification = null,
        string? shadingHex = null,
        string? colorHex = null,
        int before = 0,
        int after = 0)
    {
        var paragraphProperties = new ParagraphProperties(
            new SpacingBetweenLines
            {
                Before = before == 0 ? null : before.ToString(CultureInfo.InvariantCulture),
                After = after == 0 ? null : after.ToString(CultureInfo.InvariantCulture)
            });

        if (justification.HasValue)
        {
            paragraphProperties.Append(new Justification { Val = justification.Value });
        }

        if (!string.IsNullOrWhiteSpace(shadingHex))
        {
            paragraphProperties.Append(new Shading
            {
                Val = ShadingPatternValues.Clear,
                Fill = shadingHex,
                Color = "auto"
            });
        }

        var paragraph = new Paragraph(paragraphProperties);
        foreach (var segment in segments)
        {
            var runProperties = CreateRunProperties(sizeHalfPoints, segment.Bold, segment.Italic, segment.Strike, segment.Underline, colorHex);
            AppendSegmentText(
                paragraph,
                runProperties,
                segment.Text ?? string.Empty,
                segment.PreserveLineBreaks);
        }

        return paragraph;
    }

    private static void AppendSegmentText(
        Paragraph paragraph,
        RunProperties runProperties,
        string text,
        bool preserveLineBreaks)
    {
        var normalizedText = (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        if (!preserveLineBreaks || normalizedText.Length == 0)
        {
            paragraph.Append(new Run(runProperties.CloneNode(true), new Text(normalizedText) { Space = SpaceProcessingModeValues.Preserve }));
            return;
        }

        var lines = normalizedText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            paragraph.Append(new Run(runProperties.CloneNode(true), new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve }));
            if (i < lines.Length - 1)
            {
                paragraph.Append(new Run(runProperties.CloneNode(true), new Break()));
            }
        }
    }

    private void AppendHtmlParagraphs(
        TableCell cell,
        MainDocumentPart mainPart,
        string safeHtml,
        TextSegment? prefixSegment,
        int beforeFirst,
        int afterLast,
        string? shadingHex,
        string? textColorHex = null)
    {
        var parsed = ParseRichHtml(safeHtml);
        if (parsed.Count == 0)
        {
            return;
        }

        for (var paragraphIndex = 0; paragraphIndex < parsed.Count; paragraphIndex++)
        {
            var paragraphModel = parsed[paragraphIndex];
            var isFirst = paragraphIndex == 0;
            var isLast = paragraphIndex == parsed.Count - 1;
            var paragraph = CreateParagraphShell(
                before: isFirst ? beforeFirst : 0,
                after: isLast ? afterLast : 8,
                shadingHex: shadingHex,
                indentLevel: paragraphModel.IndentLevel);

            if (isFirst && prefixSegment.HasValue)
            {
                var segment = prefixSegment.Value;
                var prefixRunProperties = CreateRunProperties(
                    sizeHalfPoints: BaseFontHalfPoints,
                    bold: segment.Bold,
                    italic: segment.Italic,
                    strike: segment.Strike,
                    underline: segment.Underline,
                    colorHex: textColorHex);
                AppendSegmentText(paragraph, prefixRunProperties, segment.Text ?? string.Empty, segment.PreserveLineBreaks);
            }

            foreach (var token in paragraphModel.Tokens)
            {
                if (token.IsLineBreak)
                {
                    paragraph.Append(new Run(CreateRunProperties(BaseFontHalfPoints, token.Bold, token.Italic, false, token.Underline), new Break()));
                    continue;
                }

                if (string.IsNullOrEmpty(token.Text))
                {
                    continue;
                }

                var runProperties = CreateRunProperties(
                    BaseFontHalfPoints,
                    token.Bold,
                    token.Italic,
                    strike: false,
                    underline: token.Underline || !string.IsNullOrWhiteSpace(token.LinkHref),
                    colorHex: string.IsNullOrWhiteSpace(token.LinkHref) ? textColorHex : "0563C1");

                if (!string.IsNullOrWhiteSpace(token.LinkHref)
                    && Uri.TryCreate(token.LinkHref, UriKind.Absolute, out var hyperlinkUri))
                {
                    var hyperlinkRelationship = mainPart.AddHyperlinkRelationship(hyperlinkUri, true);
                    var hyperlinkRun = new Run(runProperties, new Text(token.Text) { Space = SpaceProcessingModeValues.Preserve });
                    paragraph.Append(new Hyperlink(hyperlinkRun)
                    {
                        Id = hyperlinkRelationship.Id,
                        History = OnOffValue.FromBoolean(true)
                    });
                    continue;
                }

                paragraph.Append(new Run(runProperties, new Text(token.Text) { Space = SpaceProcessingModeValues.Preserve }));
            }

            cell.Append(paragraph);
        }
    }

    private static Paragraph CreateParagraphShell(int before, int after, string? shadingHex, int indentLevel)
    {
        var paragraphProperties = new ParagraphProperties(
            new SpacingBetweenLines
            {
                Before = before == 0 ? null : before.ToString(CultureInfo.InvariantCulture),
                After = after == 0 ? null : after.ToString(CultureInfo.InvariantCulture)
            });

        if (!string.IsNullOrWhiteSpace(shadingHex))
        {
            paragraphProperties.Append(new Shading
            {
                Val = ShadingPatternValues.Clear,
                Fill = shadingHex,
                Color = "auto"
            });
        }

        if (indentLevel > 0)
        {
            paragraphProperties.Append(new Indentation
            {
                Left = (indentLevel * 420).ToString(CultureInfo.InvariantCulture)
            });
        }

        return new Paragraph(paragraphProperties);
    }

    private static IReadOnlyList<HtmlParagraphModel> ParseRichHtml(string safeHtml)
    {
        if (string.IsNullOrWhiteSpace(safeHtml))
        {
            return Array.Empty<HtmlParagraphModel>();
        }

        var normalizedForXml = NormalizeHtmlForXml(safeHtml);
        XDocument document;
        try
        {
            document = XDocument.Parse($"<root>{normalizedForXml}</root>", LoadOptions.PreserveWhitespace);
        }
        catch
        {
            return
            [
                new HtmlParagraphModel(
                    IndentLevel: 0,
                    Tokens: [new HtmlInlineToken(WebUtility.HtmlDecode(safeHtml), false, false, false, null, false)])
            ];
        }

        var paragraphs = new List<HtmlParagraphModel>();
        var root = document.Root;
        if (root is null)
        {
            return paragraphs;
        }

        foreach (var node in root.Nodes())
        {
            if (node is XElement element && element.Name.LocalName.Equals("p", StringComparison.OrdinalIgnoreCase))
            {
                var tokens = new List<HtmlInlineToken>();
                foreach (var childNode in element.Nodes())
                {
                    AppendInlineTokens(childNode, default, tokens);
                }

                paragraphs.Add(new HtmlParagraphModel(ParseIndentLevel(element.Attribute("class")?.Value), tokens));
                continue;
            }

            if (node is XElement listElement && IsListElement(listElement))
            {
                AppendListParagraphs(listElement, paragraphs, baseIndentLevel: 0);
                continue;
            }

            if (node is XText textNode)
            {
                if (string.IsNullOrWhiteSpace(textNode.Value))
                {
                    continue;
                }

                paragraphs.Add(new HtmlParagraphModel(0, [new HtmlInlineToken(textNode.Value, false, false, false, null, false)]));
                continue;
            }

            if (node is XElement otherElement)
            {
                var tokens = new List<HtmlInlineToken>();
                AppendInlineTokens(otherElement, default, tokens);
                if (tokens.Count > 0)
                {
                    paragraphs.Add(new HtmlParagraphModel(ParseIndentLevel(otherElement.Attribute("class")?.Value), tokens));
                }
            }
        }

        return paragraphs;
    }

    private static void AppendListParagraphs(XElement listElement, List<HtmlParagraphModel> paragraphs, int baseIndentLevel)
    {
        var defaultListTag = listElement.Name.LocalName.Equals("ol", StringComparison.OrdinalIgnoreCase)
            ? "ol"
            : "ul";
        var orderedIndex = 0;

        foreach (var node in listElement.Nodes())
        {
            if (node is not XElement listItemElement
                || !listItemElement.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase))
            {
                if (node is XElement nestedListElement && IsListElement(nestedListElement))
                {
                    AppendListParagraphs(nestedListElement, paragraphs, baseIndentLevel + 1);
                }

                continue;
            }

            var resolvedListTag = ResolveListTag(defaultListTag, listItemElement);
            var marker = resolvedListTag == "ol"
                ? $"{++orderedIndex}. "
                : "• ";
            if (resolvedListTag != "ol")
            {
                orderedIndex = 0;
            }

            var tokens = new List<HtmlInlineToken>
            {
                new(marker, false, false, false, null, false)
            };

            foreach (var child in listItemElement.Nodes())
            {
                if (child is XElement nestedList && IsListElement(nestedList))
                {
                    continue;
                }

                AppendInlineTokens(child, default, tokens);
            }

            var itemIndentLevel = baseIndentLevel + ParseIndentLevel(listItemElement.Attribute("class")?.Value);
            paragraphs.Add(new HtmlParagraphModel(itemIndentLevel, tokens));

            foreach (var nestedList in listItemElement.Elements().Where(IsListElement))
            {
                AppendListParagraphs(nestedList, paragraphs, itemIndentLevel + 1);
            }
        }
    }

    private static bool IsListElement(XElement element)
    {
        return element.Name.LocalName.Equals("ul", StringComparison.OrdinalIgnoreCase)
            || element.Name.LocalName.Equals("ol", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveListTag(string defaultListTag, XElement listItemElement)
    {
        var listMode = (listItemElement.Attribute("data-list")?.Value ?? string.Empty).Trim();
        if (listMode.Equals("bullet", StringComparison.OrdinalIgnoreCase))
        {
            return "ul";
        }

        if (listMode.Equals("ordered", StringComparison.OrdinalIgnoreCase))
        {
            return "ol";
        }

        return defaultListTag;
    }

    private static void AppendInlineTokens(XNode node, HtmlStyleState style, List<HtmlInlineToken> target)
    {
        if (node is XText textNode)
        {
            if (textNode.Value.Length > 0)
            {
                target.Add(new HtmlInlineToken(textNode.Value, style.Bold, style.Italic, style.Underline, style.LinkHref, false));
            }
            return;
        }

        if (node is not XElement element)
        {
            return;
        }

        var localName = element.Name.LocalName;
        if (localName.Equals("br", StringComparison.OrdinalIgnoreCase))
        {
            target.Add(new HtmlInlineToken(string.Empty, style.Bold, style.Italic, style.Underline, style.LinkHref, true));
            return;
        }

        var nextStyle = style;
        if (localName.Equals("strong", StringComparison.OrdinalIgnoreCase) || localName.Equals("b", StringComparison.OrdinalIgnoreCase))
        {
            nextStyle = nextStyle with { Bold = true };
        }
        else if (localName.Equals("em", StringComparison.OrdinalIgnoreCase) || localName.Equals("i", StringComparison.OrdinalIgnoreCase))
        {
            nextStyle = nextStyle with { Italic = true };
        }
        else if (localName.Equals("u", StringComparison.OrdinalIgnoreCase))
        {
            nextStyle = nextStyle with { Underline = true };
        }
        else if (localName.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            var href = (element.Attribute("href")?.Value ?? string.Empty).Trim();
            nextStyle = nextStyle with
            {
                LinkHref = string.IsNullOrWhiteSpace(href) ? null : href
            };
        }

        foreach (var child in element.Nodes())
        {
            AppendInlineTokens(child, nextStyle, target);
        }
    }

    private static int ParseIndentLevel(string? classValue)
    {
        if (string.IsNullOrWhiteSpace(classValue))
        {
            return 0;
        }

        var match = IndentClassRegex().Match(classValue);
        if (!match.Success)
        {
            return 0;
        }

        return int.TryParse(match.Groups["level"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)
            ? Math.Clamp(level, 0, 8)
            : 0;
    }

    private static string NormalizeHtmlForXml(string html)
    {
        var normalized = BreakTagRegex().Replace(html, "<br />");
        return normalized.Replace("&nbsp;", "&#160;", StringComparison.OrdinalIgnoreCase);
    }

    private static RunProperties CreateRunProperties(int sizeHalfPoints, bool bold, bool italic, bool strike, bool underline, string? colorHex = null)
    {
        var runProperties = new RunProperties(
            new RunFonts
            {
                Ascii = "Times New Roman",
                HighAnsi = "Times New Roman",
                EastAsia = "Times New Roman",
                ComplexScript = "Times New Roman"
            },
            new FontSize { Val = sizeHalfPoints.ToString(CultureInfo.InvariantCulture) });

        if (bold)
        {
            runProperties.Append(new Bold());
        }

        if (italic)
        {
            runProperties.Append(new Italic());
        }

        if (strike)
        {
            runProperties.Append(new Strike());
        }

        if (underline)
        {
            runProperties.Append(new Underline { Val = UnderlineValues.Single });
        }

        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            runProperties.Append(new Color { Val = colorHex });
        }

        return runProperties;
    }

    private static Paragraph CreateParagraph(
        string text,
        bool bold = false,
        bool italic = false,
        bool strike = false,
        int sizeHalfPoints = BaseFontHalfPoints,
        JustificationValues? justification = null,
        string? shadingHex = null,
        string? colorHex = null,
        int before = 0,
        int after = 0,
        bool preserveLineBreaks = false)
    {
        var paragraphProperties = new ParagraphProperties(
            new SpacingBetweenLines
            {
                Before = before == 0 ? null : before.ToString(CultureInfo.InvariantCulture),
                After = after == 0 ? null : after.ToString(CultureInfo.InvariantCulture)
            });

        if (justification.HasValue)
        {
            paragraphProperties.Append(new Justification { Val = justification.Value });
        }

        if (!string.IsNullOrWhiteSpace(shadingHex))
        {
            paragraphProperties.Append(new Shading
            {
                Val = ShadingPatternValues.Clear,
                Fill = shadingHex,
                Color = "auto"
            });
        }

        var paragraph = new Paragraph(paragraphProperties);
        var runProperties = CreateRunProperties(sizeHalfPoints, bold, italic, strike, underline: false, colorHex);
        var normalizedText = (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        if (!preserveLineBreaks || normalizedText.Length == 0)
        {
            paragraph.Append(new Run(runProperties, new Text(normalizedText) { Space = SpaceProcessingModeValues.Preserve }));
            return paragraph;
        }

        var lines = normalizedText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineRun = new Run(runProperties.CloneNode(true), new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.Append(lineRun);
            if (i < lines.Length - 1)
            {
                paragraph.Append(new Run(runProperties.CloneNode(true), new Break()));
            }
        }

        return paragraph;
    }

    private static Paragraph CreateSpacerParagraph(int after)
    {
        return CreateParagraph(string.Empty, sizeHalfPoints: BaseFontHalfPoints, before: 0, after: after);
    }

    private static int CategoryOrder(string? category)
    {
        var normalized = (category ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("info", StringComparison.Ordinal))
        {
            return 1;
        }

        if (normalized.Contains("rozh", StringComparison.Ordinal))
        {
            return 2;
        }

        if (normalized.Contains("ukol", StringComparison.Ordinal) || normalized.Contains("úkol", StringComparison.Ordinal))
        {
            return 3;
        }

        return 4;
    }

    private static string? NormalizeHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('#') && normalized.Length == 7)
        {
            return normalized[1..].ToUpperInvariant();
        }

        var match = RgbRegex().Match(normalized);
        if (!match.Success)
        {
            return null;
        }

        var red = ClampColor(match.Groups["r"].Value);
        var green = ClampColor(match.Groups["g"].Value);
        var blue = ClampColor(match.Groups["b"].Value);
        return $"{red:X2}{green:X2}{blue:X2}";
    }

    private static int ClampColor(string raw)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        return Math.Clamp(value, 0, 255);
    }

    [GeneratedRegex(@"^rgba?\(\s*(?<r>\d{1,3})\s*,\s*(?<g>\d{1,3})\s*,\s*(?<b>\d{1,3})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RgbRegex();

    [GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakTagRegex();

    [GeneratedRegex(@"(?:^|\s)ql-indent-(?<level>\d+)(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex IndentClassRegex();

    private readonly record struct TextSegment(
        string Text,
        bool Bold = false,
        bool Italic = false,
        bool Underline = false,
        bool Strike = false,
        bool PreserveLineBreaks = false);

    private readonly record struct HtmlInlineToken(
        string Text,
        bool Bold,
        bool Italic,
        bool Underline,
        string? LinkHref,
        bool IsLineBreak);

    private readonly record struct HtmlParagraphModel(
        int IndentLevel,
        IReadOnlyList<HtmlInlineToken> Tokens);

    private readonly record struct HtmlStyleState(
        bool Bold = false,
        bool Italic = false,
        bool Underline = false,
        string? LinkHref = null);
}
