using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Export;

public sealed partial class OpenXmlWordRecordCommentsCellWriter(
    IRichTextContentService richTextContentService,
    IWordExportRichHtmlParagraphWriter richHtmlParagraphWriter,
    IWordExportRecordHeaderWriter recordHeaderWriter) : IWordExportRecordCommentsCellWriter
{
    public void Append(TableCell cell, MainDocumentPart mainPart, PdfExportRecordViewModel record)
    {
        recordHeaderWriter.Append(cell, mainPart, record);

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
                richHtmlParagraphWriter.AppendHtmlParagraphs(
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
}
