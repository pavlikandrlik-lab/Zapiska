using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Export;

public sealed class OpenXmlWordRecordHeaderWriter(
    IRichTextContentService richTextContentService,
    IWordExportRichHtmlParagraphWriter richHtmlParagraphWriter) : IWordExportRecordHeaderWriter
{
    public void Append(TableCell cell, MainDocumentPart mainPart, PdfExportRecordViewModel record)
    {
        var code = string.IsNullOrWhiteSpace(record.TypUkoluKod)
            ? (string.IsNullOrWhiteSpace(record.KategorieKod) ? "-" : record.KategorieKod)
            : record.TypUkoluKod;
        var isPaused = (record.Stav ?? string.Empty).Contains("pozastav", StringComparison.CurrentCultureIgnoreCase);
        var pausedFill = isPaused ? "FDF4E8" : null;

        cell.Append(OpenXmlWordElements.CreateParagraph(
            $"{code}{record.CisloViditelne} - {record.Nazev}",
            bold: true,
            sizeHalfPoints: OpenXmlWordElements.RecordTitleHalfPoints,
            before: 40,
            after: 40,
            shadingHex: pausedFill));

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
                richHtmlParagraphWriter.AppendHtmlParagraphs(
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
}
