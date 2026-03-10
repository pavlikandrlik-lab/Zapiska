using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PmTracker.Web.Services.Common;

public sealed partial class RichTextContentService : IRichTextContentService
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p",
        "br",
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
                builder.Append(HtmlEncoder.Default.Encode(textNode.Value));
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
                .Append(HtmlEncoder.Default.Encode(href))
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

        builder.Append('<').Append(tagName).Append('>');
        foreach (var child in element.Nodes())
        {
            AppendSanitizedNode(child, builder);
        }
        builder.Append("</").Append(tagName).Append('>');
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

    private static string NormalizeHtmlForXml(string html)
    {
        var normalized = BreakTagRegex().Replace(html, "<br />");
        return normalized.Replace("&nbsp;", "&#160;", StringComparison.OrdinalIgnoreCase);
    }

    private static string ConvertPlainTextToHtml(string value)
    {
        var normalized = NormalizeLineEndings(value);
        var encodedLines = normalized
            .Split('\n')
            .Select(HtmlEncoder.Default.Encode);
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

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex AnyTagRegex();
}
