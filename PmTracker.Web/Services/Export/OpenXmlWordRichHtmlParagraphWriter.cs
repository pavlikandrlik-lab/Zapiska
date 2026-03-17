using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PmTracker.Web.Services.Export;

public sealed partial class OpenXmlWordRichHtmlParagraphWriter : IWordExportRichHtmlParagraphWriter
{
    public void AppendHtmlParagraphs(
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

    [GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakTagRegex();

    [GeneratedRegex(@"(?:^|\s)ql-indent-(?<level>\d+)(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex IndentClassRegex();

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
