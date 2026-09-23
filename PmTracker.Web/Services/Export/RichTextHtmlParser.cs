using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PmTracker.Web.Services.Export;

/// <summary>Jeden běh textu s formátováním.</summary>
public readonly record struct RichTextToken(
    string Text,
    bool Bold,
    bool Italic,
    bool Underline,
    string? LinkHref,
    bool IsLineBreak);

/// <summary>Druh seznamu, ke kterému odstavec patří.</summary>
public enum RichTextListKind
{
    None,
    Bullet,
    Ordered,
}

/// <summary>
/// Jeden odstavec: úroveň odsazení a běhy textu. U položky seznamu nese i druh a identitu
/// seznamu (odstavce téhož seznamu mají stejné ListId) a první token je vždy značka.
/// </summary>
public readonly record struct RichTextParagraph(
    int IndentLevel,
    IReadOnlyList<RichTextToken> Tokens,
    RichTextListKind ListKind = RichTextListKind.None,
    int ListId = 0);

/// <summary>
/// Převádí sanitizované HTML z rich text editoru na odstavce a běhy textu.
///
/// Vytaženo 2026-09-08 z OpenXmlWordExportService, kde bylo privátní — výzva potřebuje
/// totéž. Sdílí se schválně jen parsování: skládání odstavců zůstává u každého exportu
/// zvlášť, protože tisk záznamu sází do buňky tabulky a výzva do těla dokumentu.
///
/// Třída je partial kvůli [GeneratedRegex] — source-generated regexy vyžadují partial
/// typ. Oba regexy sem patří, používá je jen parser (RgbRegex u barev zůstal v exportu).
/// </summary>
public static partial class RichTextHtmlParser
{
    private readonly record struct RichTextStyleState(
        bool Bold = false,
        bool Italic = false,
        bool Underline = false,
        string? LinkHref = null);

    /// <summary>Sdílené přes celé Parse — každý seznam dostane vlastní ListId.</summary>
    private sealed class ListCounter
    {
        public int Posledni { get; set; }
    }

    [GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakTagRegex();

    [GeneratedRegex(@"(?:^|\s)ql-indent-(?<level>\d+)(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex IndentClassRegex();

    public static IReadOnlyList<RichTextParagraph> Parse(string safeHtml)
    {
        if (string.IsNullOrWhiteSpace(safeHtml))
        {
            return Array.Empty<RichTextParagraph>();
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
                new RichTextParagraph(
                    IndentLevel: 0,
                    Tokens: [new RichTextToken(WebUtility.HtmlDecode(safeHtml), false, false, false, null, false)])
            ];
        }

        var paragraphs = new List<RichTextParagraph>();
        var root = document.Root;
        if (root is null)
        {
            return paragraphs;
        }

        var listCounter = new ListCounter();
        foreach (var node in root.Nodes())
        {
            if (node is XElement element && element.Name.LocalName.Equals("p", StringComparison.OrdinalIgnoreCase))
            {
                var tokens = new List<RichTextToken>();
                foreach (var childNode in element.Nodes())
                {
                    AppendInlineTokens(childNode, default, tokens);
                }

                paragraphs.Add(new RichTextParagraph(ParseIndentLevel(element.Attribute("class")?.Value), tokens));
                continue;
            }

            if (node is XElement listElement && IsListElement(listElement))
            {
                AppendListParagraphs(listElement, paragraphs, baseIndentLevel: 0, listCounter);
                continue;
            }

            if (node is XText textNode)
            {
                if (string.IsNullOrWhiteSpace(textNode.Value))
                {
                    continue;
                }

                paragraphs.Add(new RichTextParagraph(0, [new RichTextToken(textNode.Value, false, false, false, null, false)]));
                continue;
            }

            if (node is XElement otherElement)
            {
                var tokens = new List<RichTextToken>();
                AppendInlineTokens(otherElement, default, tokens);
                if (tokens.Count > 0)
                {
                    paragraphs.Add(new RichTextParagraph(ParseIndentLevel(otherElement.Attribute("class")?.Value), tokens));
                }
            }
        }

        return paragraphs;
    }

    private static void AppendListParagraphs(XElement listElement, List<RichTextParagraph> paragraphs, int baseIndentLevel, ListCounter counter)
    {
        var defaultListTag = listElement.Name.LocalName.Equals("ol", StringComparison.OrdinalIgnoreCase)
            ? "ol"
            : "ul";
        var orderedIndex = 0;
        string? predchoziTag = null;
        var listId = 0;

        foreach (var node in listElement.Nodes())
        {
            if (node is not XElement listItemElement
                || !listItemElement.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase))
            {
                if (node is XElement nestedListElement && IsListElement(nestedListElement))
                {
                    AppendListParagraphs(nestedListElement, paragraphs, baseIndentLevel + 1, counter);
                }

                continue;
            }

            var resolvedListTag = ResolveListTag(defaultListTag, listItemElement);

            // Quill 2 dává odrážky i čísla do jednoho <ol> a liší je data-list. Změna druhu
            // uvnitř elementu je pro Word nový seznam — číslování musí začít znovu od 1.
            if (resolvedListTag != predchoziTag)
            {
                listId = ++counter.Posledni;
                predchoziTag = resolvedListTag;
            }

            var marker = resolvedListTag == "ol"
                ? $"{++orderedIndex}. "
                : "• ";
            if (resolvedListTag != "ol")
            {
                orderedIndex = 0;
            }

            var tokens = new List<RichTextToken>
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
            paragraphs.Add(new RichTextParagraph(
                itemIndentLevel,
                tokens,
                resolvedListTag == "ol" ? RichTextListKind.Ordered : RichTextListKind.Bullet,
                listId));

            foreach (var nestedList in listItemElement.Elements().Where(IsListElement))
            {
                AppendListParagraphs(nestedList, paragraphs, itemIndentLevel + 1, counter);
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

    private static void AppendInlineTokens(XNode node, RichTextStyleState style, List<RichTextToken> target)
    {
        if (node is XText textNode)
        {
            if (textNode.Value.Length > 0)
            {
                target.Add(new RichTextToken(textNode.Value, style.Bold, style.Italic, style.Underline, style.LinkHref, false));
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
            target.Add(new RichTextToken(string.Empty, style.Bold, style.Italic, style.Underline, style.LinkHref, true));
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
}
