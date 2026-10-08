using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PmTracker.Web.Services.Common;

public sealed partial class RichTextContentService : IRichTextContentService
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p",
        "br",
        "ul",
        "ol",
        "li",
        "strong",
        "b",
        "em",
        "i",
        "u",
        "a"
    };

    private static readonly HashSet<string> AllowedIndentClasses = new(StringComparer.Ordinal)
    {
        "ql-indent-1",
        "ql-indent-2",
        "ql-indent-3",
        "ql-indent-4",
        "ql-indent-5",
        "ql-indent-6",
        "ql-indent-7",
        "ql-indent-8"
    };

    public string NormalizeForStorage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = NormalizeLineEndings(value);
        if (!LooksLikeHtml(normalized))
        {
            return normalized;
        }

        var sanitized = SanitizeHtml(normalized);
        return HasVisibleTextInHtml(sanitized) ? sanitized : string.Empty;
    }

    public string ToSafeHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = NormalizeLineEndings(value);
        var html = LooksLikeHtml(normalized)
            ? SanitizeHtml(normalized)
            : ConvertPlainTextToHtml(normalized);

        return HasVisibleTextInHtml(html) ? html : string.Empty;
    }

    public string ToPlainText(string? value)
    {
        var html = ToSafeHtml(value);
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withBreaks = BreakTagRegex().Replace(html, "\n");
        withBreaks = ParagraphClosingTagRegex().Replace(withBreaks, "\n");
        withBreaks = ListItemClosingTagRegex().Replace(withBreaks, "\n");
        var withoutTags = AnyTagRegex().Replace(withBreaks, string.Empty);
        var decoded = NormalizeLineEndings(WebUtility.HtmlDecode(withoutTags));
        return decoded.Trim();
    }

    public bool HasVisibleText(string? value)
    {
        return !string.IsNullOrWhiteSpace(ToPlainText(value));
    }

    private static string SanitizeHtml(string html)
    {
        var normalizedForXml = NormalizeHtmlForXml(html);
        XDocument document;
        try
        {
            document = XDocument.Parse($"<root>{normalizedForXml}</root>", LoadOptions.PreserveWhitespace);
        }
        catch
        {
            return ConvertPlainTextToHtml(html);
        }

        var root = document.Root;
        if (root is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var node in root.Nodes())
        {
            AppendSanitizedNode(node, builder);
        }

        return builder.ToString().Trim();
    }

    private static void AppendSanitizedNode(XNode node, StringBuilder builder)
    {
        if (node is XText textNode)
        {
            if (!string.IsNullOrEmpty(textNode.Value))
            {
                builder.Append(EncodeHtml(textNode.Value));
            }
            return;
        }

        if (node is not XElement element)
        {
            return;
        }

        var tagName = element.Name.LocalName.Trim().ToLowerInvariant();
        if (!AllowedTags.Contains(tagName))
        {
            foreach (var child in element.Nodes())
            {
                AppendSanitizedNode(child, builder);
            }
            return;
        }

        if (tagName == "br")
        {
            builder.Append("<br>");
            return;
        }

        if (tagName == "a")
        {
            var href = NormalizeHref(element.Attribute("href")?.Value);
            if (href is null)
            {
                foreach (var child in element.Nodes())
                {
                    AppendSanitizedNode(child, builder);
                }
                return;
            }

            builder.Append("<a href=\"")
                .Append(EncodeHtml(href))
                .Append("\">");
            foreach (var child in element.Nodes())
            {
                AppendSanitizedNode(child, builder);
            }
            builder.Append("</a>");
            return;
        }

        if (tagName == "p")
        {
            var indentClass = ExtractAllowedIndentClass(element.Attribute("class")?.Value);
            if (indentClass is null)
            {
                builder.Append("<p>");
            }
            else
            {
                builder.Append("<p class=\"")
                    .Append(indentClass)
                    .Append("\">");
            }

            foreach (var child in element.Nodes())
            {
                AppendSanitizedNode(child, builder);
            }
            builder.Append("</p>");
            return;
        }

        if (tagName == "ul" || tagName == "ol")
        {
            AppendSanitizedList(element, tagName, builder);
            return;
        }

        if (tagName == "li")
        {
            AppendSanitizedListItem(element, builder);
            return;
        }

        builder.Append('<').Append(tagName).Append('>');
        foreach (var child in element.Nodes())
        {
            AppendSanitizedNode(child, builder);
        }
        builder.Append("</").Append(tagName).Append('>');
    }

    private static void AppendSanitizedList(XElement listElement, string defaultListTag, StringBuilder builder)
    {
        var currentListTag = string.Empty;
        foreach (var node in listElement.Nodes())
        {
            if (node is XElement listItemElement
                && listItemElement.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedListTag = ResolveListTag(defaultListTag, listItemElement);
                if (!string.Equals(currentListTag, resolvedListTag, StringComparison.Ordinal))
                {
                    if (currentListTag.Length > 0)
                    {
                        builder.Append("</").Append(currentListTag).Append('>');
                    }

                    builder.Append('<').Append(resolvedListTag).Append('>');
                    currentListTag = resolvedListTag;
                }

                AppendSanitizedListItem(listItemElement, builder);
                continue;
            }

            if (currentListTag.Length > 0)
            {
                builder.Append("</").Append(currentListTag).Append('>');
                currentListTag = string.Empty;
            }

            AppendSanitizedNode(node, builder);
        }

        if (currentListTag.Length > 0)
        {
            builder.Append("</").Append(currentListTag).Append('>');
        }
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

    private static void AppendSanitizedListItem(XElement listItemElement, StringBuilder builder)
    {
        var indentClass = ExtractAllowedIndentClass(listItemElement.Attribute("class")?.Value);
        if (indentClass is null)
        {
            builder.Append("<li>");
        }
        else
        {
            builder.Append("<li class=\"")
                .Append(indentClass)
                .Append("\">");
        }

        foreach (var child in listItemElement.Nodes())
        {
            AppendSanitizedNode(child, builder);
        }

        builder.Append("</li>");
    }

    private static string? ExtractAllowedIndentClass(string? classValue)
    {
        if (string.IsNullOrWhiteSpace(classValue))
        {
            return null;
        }

        var tokens = classValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (AllowedIndentClasses.Contains(token))
            {
                return token;
            }
        }

        return null;
    }

    private static string? NormalizeHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var trimmed = href.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.ToString();
    }

    /// <summary>
    /// Upraví platné HTML tak, aby ho přijal XML parser. Bez toho se HTML odjinud než
    /// z editoru (Word, starší data) celé zobrazilo jako text i se značkami a značky šly
    /// i do čistého textu pro hledání (drobnost z review 2026-10-08). Co ani pak není
    /// XML, zůstává pro záložní cestu — je to text, který HTML jen připomíná.
    /// </summary>
    private static string NormalizeHtmlForXml(string html)
    {
        var normalized = BreakTagRegex().Replace(html, "<br />");
        normalized = normalized.Replace("&nbsp;", "&#160;", StringComparison.OrdinalIgnoreCase);
        // Značky s předponou z Wordu (<o:p>) — XML je odmítne kvůli nedeklarované předponě.
        normalized = PrefixedTagRegex().Replace(normalized, string.Empty);
        // Prázdné značky (<img>, <hr>) v HTML nemají uzavírací značku, v XML ji musí mít.
        normalized = VoidTagRegex().Replace(normalized, "<${name}${attrs} />");
        // Názvy značek na malá písmena — HTML velikost písmen nerozlišuje, XML ano (<P>…</p>).
        normalized = TagNameRegex().Replace(normalized,
            match => match.Groups["open"].Value + match.Groups["name"].Value.ToLowerInvariant());
        return NamedEntityRegex().Replace(normalized, DecodeNamedEntity);
    }

    // Pojmenované entity HTML (&ndash;, &copy;) XML nezná — na znak. Pět entit, které XML zná
    // a které nesou význam (&amp; &lt; …), zůstává; neznámá entita taky (dál neprojde parserem).
    private static string DecodeNamedEntity(Match match)
    {
        if (XmlEntityNames.Contains(match.Groups["name"].Value))
        {
            return match.Value;
        }

        var decoded = WebUtility.HtmlDecode(match.Value);
        return decoded == match.Value || decoded.AsSpan().IndexOfAny("&<>\"'") >= 0
            ? match.Value
            : decoded;
    }

    private static readonly HashSet<string> XmlEntityNames = new(StringComparer.Ordinal)
    {
        "amp", "lt", "gt", "quot", "apos"
    };

    /// <summary>
    /// Kóduje jen znaky, které v HTML něco znamenají (&amp;, &lt;, &gt; a uvozovky
    /// v atributu). Písmena i ostatní znaky zůstávají, jak jsou — HtmlEncoder.Default
    /// z „ř" dělal &amp;#x159; a LIKE v databázi pak slova s diakritikou nenašel.
    /// Rich text sloupce jsou od db_upgrade_1_4_6 NVARCHAR(MAX), takže se nic neztratí.
    /// </summary>
    private static string EncodeHtml(string value)
    {
        if (value.AsSpan().IndexOfAny("&<>\"") < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 16);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                default: builder.Append(ch); break;
            }
        }

        return builder.ToString();
    }

    private static string ConvertPlainTextToHtml(string value)
    {
        var normalized = NormalizeLineEndings(value);
        var encodedLines = normalized
            .Split('\n')
            .Select(EncodeHtml);
        return $"<p>{string.Join("<br>", encodedLines)}</p>";
    }

    private static bool HasVisibleTextInHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var withBreaks = BreakTagRegex().Replace(html, "\n");
        withBreaks = ParagraphClosingTagRegex().Replace(withBreaks, "\n");
        withBreaks = ListItemClosingTagRegex().Replace(withBreaks, "\n");
        var withoutTags = AnyTagRegex().Replace(withBreaks, string.Empty);
        var decoded = WebUtility.HtmlDecode(withoutTags);

        foreach (var c in decoded)
        {
            if (!char.IsWhiteSpace(c))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeHtml(string value)
    {
        return HtmlTagRegex().IsMatch(value);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"<\s*/?\s*[a-zA-Z][^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakTagRegex();

    [GeneratedRegex(@"</\s*p\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ParagraphClosingTagRegex();

    [GeneratedRegex(@"</\s*li\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListItemClosingTagRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex AnyTagRegex();

    [GeneratedRegex(@"<\s*/?\s*[A-Za-z][\w.-]*:[\w.-]+[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex PrefixedTagRegex();

    [GeneratedRegex(@"<\s*(?<name>img|hr|input|meta|link|wbr|col|area|source|embed|param|track)\b(?<attrs>[^>]*?)\s*/?\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VoidTagRegex();

    [GeneratedRegex(@"(?<open></?)(?<name>[A-Za-z][A-Za-z0-9]*)", RegexOptions.CultureInvariant)]
    private static partial Regex TagNameRegex();

    [GeneratedRegex(@"&(?<name>[A-Za-z][A-Za-z0-9]*);", RegexOptions.CultureInvariant)]
    private static partial Regex NamedEntityRegex();
}
