using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

/// <summary>
/// Partial OpenXmlWordExportService — records table rendering:
/// záznam, komentáře, external links, lidé, historie termínů a HTML parser.
/// </summary>
public sealed partial class OpenXmlWordExportService
{
    private void AppendRecordsSection(Body body, MainDocumentPart mainPart, IReadOnlyList<PdfExportRecordViewModel> records)
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
            OpenXmlWordElements.CreateCell("Záznamy a vyjádření", bold: true, fillColor: "F3F4F6"),
            OpenXmlWordElements.CreateCell("Osoby", bold: true, fillColor: "F3F4F6"),
            OpenXmlWordElements.CreateCell("Termíny", bold: true, fillColor: "F3F4F6"));
    }

    private static TableRow CreateSingleCellRow(string text, int gridSpan, string? fillColor = null, bool italic = false)
    {
        var cellProps = new TableCellProperties(new GridSpan { Val = gridSpan });
        if (!string.IsNullOrWhiteSpace(fillColor))
        {
            cellProps.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = fillColor, Color = "auto" });
        }

        var cell = new TableCell(cellProps);
        cell.Append(OpenXmlWordElements.CreateParagraph(text, bold: !italic, italic: italic, sizeHalfPoints: italic ? 19 : 22, before: 40, after: 40));
        return new TableRow(cell);
    }

    private TableRow CreateRecordRow(MainDocumentPart mainPart, PdfExportRecordViewModel record)
    {
        var recordFillColor = record.IsPaused ? PausedRecordFillHex : null;
        var commentsCell = CreateRecordCell("8100", recordFillColor);
        var peopleCell = CreateRecordCell("2100", recordFillColor);
        var deadlinesCell = CreateRecordCell("1600", recordFillColor);

        AppendComments(commentsCell, mainPart, record);
        AppendPeople(peopleCell, record);
        AppendDeadlines(deadlinesCell, record);

        return new TableRow(commentsCell, peopleCell, deadlinesCell);
    }

    private static TableCell CreateRecordCell(string width, string? fillColor)
    {
        var properties = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = width });
        if (!string.IsNullOrWhiteSpace(fillColor))
        {
            properties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = fillColor, Color = "auto" });
        }

        return new TableCell(properties);
    }

    private void AppendComments(TableCell cell, MainDocumentPart mainPart, PdfExportRecordViewModel record)
    {
        AppendRecordHeader(cell, mainPart, record);

        foreach (var comment in record.Vyjadreni)
        {
            var meetingRef = comment.JednaniCislo.HasValue
                ? $"jednání č. {comment.JednaniCislo}{(comment.JednaniDatum.HasValue ? $" ({comment.JednaniDatum.Value:dd.MM.yyyy})" : string.Empty)}"
                : "jednání";
            var titleText = $"{meetingRef} | {comment.Autor} | {comment.Datum:dd.MM.yyyy}";
            var commentTextColor = NormalizeHexColor(comment.HighlightColor);
            cell.Append(OpenXmlWordElements.CreateParagraph(
                titleText,
                bold: true,
                sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints,
                before: 20,
                after: 0,
                colorHex: commentTextColor));
            var safeCommentHtml = richTextContentService.ToSafeHtml(comment.Text);
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

    private void AppendRecordHeader(TableCell cell, MainDocumentPart mainPart, PdfExportRecordViewModel record)
    {
        var code = string.IsNullOrWhiteSpace(record.TypUkoluKod)
            ? (string.IsNullOrWhiteSpace(record.KategorieKod) ? "-" : record.KategorieKod)
            : record.TypUkoluKod;

        cell.Append(OpenXmlWordElements.CreateParagraph(
            $"{code}{record.CisloViditelne} - {record.Nazev}",
            bold: true,
            sizeHalfPoints: OpenXmlWordElements.RecordTitleHalfPoints,
            before: 40,
            after: 40));

        cell.Append(OpenXmlWordElements.CreateRichParagraph(
            new[]
            {
                new OpenXmlWordElements.TextSegment("Stav: ", Bold: true),
                new OpenXmlWordElements.TextSegment(string.IsNullOrWhiteSpace(record.Stav) ? "-" : record.Stav),
                new OpenXmlWordElements.TextSegment(" | "),
                new OpenXmlWordElements.TextSegment("Typ úkolu: ", Bold: true),
                new OpenXmlWordElements.TextSegment(string.IsNullOrWhiteSpace(record.TypUkolu) ? "-" : record.TypUkolu!)
            },
            sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints,
            before: 0,
            after: 25));

        if (!string.IsNullOrWhiteSpace(record.Cil))
        {
            cell.Append(OpenXmlWordElements.CreateRichParagraph(
                new[]
                {
                    new OpenXmlWordElements.TextSegment("Cíl: ", Bold: true, Italic: true),
                    new OpenXmlWordElements.TextSegment(record.Cil, Italic: true, PreserveLineBreaks: true)
                },
                sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints,
                before: 0,
                after: 25));
        }

        if (!string.IsNullOrWhiteSpace(record.Popis))
        {
            var safeDescriptionHtml = richTextContentService.ToSafeHtml(record.Popis);
            if (!string.IsNullOrWhiteSpace(safeDescriptionHtml))
            {
                AppendHtmlParagraphs(
                    cell,
                    mainPart,
                    safeDescriptionHtml,
                    new OpenXmlWordElements.TextSegment("Popis: ", Bold: true),
                    beforeFirst: 0,
                    afterLast: 25,
                    shadingHex: null);
            }
        }

        if (record.ExterniVazby.Count > 0)
        {
            for (var index = 0; index < record.ExterniVazby.Count; index++)
            {
                var (header, details) = SplitExternalLinkDisplay(record.ExterniVazby[index]);
                cell.Append(OpenXmlWordElements.CreateRichParagraph(
                    new[]
                    {
                        new OpenXmlWordElements.TextSegment(header, Bold: true),
                        new OpenXmlWordElements.TextSegment(details)
                    },
                    sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints,
                    before: 0,
                    after: index == record.ExterniVazby.Count - 1 ? 45 : 20));
            }
        }
    }

    private static (string Header, string Details) SplitExternalLinkDisplay(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        var detailsIndex = normalized.IndexOf(" (", StringComparison.Ordinal);
        if (detailsIndex <= 0 || !normalized.EndsWith(')'))
        {
            return (normalized, string.Empty);
        }

        return (normalized[..detailsIndex], normalized[detailsIndex..]);
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

    private void AppendPeople(TableCell cell, PdfExportRecordViewModel record)
    {
        cell.Append(OpenXmlWordElements.CreateRichParagraph(
            new[]
            {
                new OpenXmlWordElements.TextSegment("Vlastník: ", Bold: true),
                new OpenXmlWordElements.TextSegment(record.Vlastnik)
            },
            sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints,
            before: 40,
            after: 30));

        if (record.Spoluprace.Count == 0)
        {
            return;
        }

        cell.Append(OpenXmlWordElements.CreateParagraph("Spolupráce:", bold: true, sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints, before: 20, after: 20));
        foreach (var person in record.Spoluprace)
        {
            cell.Append(OpenXmlWordElements.CreateParagraph(person, sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints, before: 0, after: 20));
        }
    }

    private void AppendDeadlines(TableCell cell, PdfExportRecordViewModel record)
    {
        cell.Append(OpenXmlWordElements.CreateRichParagraph(
            new[]
            {
                new OpenXmlWordElements.TextSegment("Založeno: ", Bold: true),
                new OpenXmlWordElements.TextSegment(record.DatumZalozeni.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture))
            },
            sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints,
            before: 40,
            after: 20));

        cell.Append(OpenXmlWordElements.CreateRichParagraph(
            new[]
            {
                new OpenXmlWordElements.TextSegment("Aktuální termín: ", Bold: true),
                new OpenXmlWordElements.TextSegment(record.Termin.HasValue ? record.Termin.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : "-")
            },
            sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints,
            before: 0,
            after: 20));

        if (record.HistorieTerminu.Count == 0)
        {
            return;
        }

        cell.Append(OpenXmlWordElements.CreateParagraph("Historie termínů:", bold: true, sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints, before: 20, after: 20));
        foreach (var date in record.HistorieTerminu.OrderByDescending(x => x))
        {
            cell.Append(OpenXmlWordElements.CreateParagraph(date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), strike: true, sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints, before: 0, after: 20));
        }
    }

    private void AppendHtmlParagraphs(
        TableCell cell,
        MainDocumentPart mainPart,
        string safeHtml,
        OpenXmlWordElements.TextSegment? prefixSegment,
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
                var prefixRunProperties = OpenXmlWordElements.CreateRunProperties(
                    sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints,
                    bold: segment.Bold,
                    italic: segment.Italic,
                    strike: segment.Strike,
                    underline: segment.Underline,
                    colorHex: textColorHex);
                OpenXmlWordElements.AppendSegmentText(paragraph, prefixRunProperties, segment.Text ?? string.Empty, segment.PreserveLineBreaks);
            }

            foreach (var token in paragraphModel.Tokens)
            {
                if (token.IsLineBreak)
                {
                    paragraph.Append(new Run(OpenXmlWordElements.CreateRunProperties(OpenXmlWordElements.BaseFontHalfPoints, token.Bold, token.Italic, false, token.Underline), new Break()));
                    continue;
                }

                if (string.IsNullOrEmpty(token.Text))
                {
                    continue;
                }

                var runProperties = OpenXmlWordElements.CreateRunProperties(
                    OpenXmlWordElements.BaseFontHalfPoints,
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
}
