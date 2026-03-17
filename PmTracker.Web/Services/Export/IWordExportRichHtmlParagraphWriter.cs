using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PmTracker.Web.Services.Export;

public interface IWordExportRichHtmlParagraphWriter
{
    void AppendHtmlParagraphs(
        TableCell cell,
        MainDocumentPart mainPart,
        string safeHtml,
        OpenXmlWordElements.TextSegment? prefixSegment,
        int beforeFirst,
        int afterLast,
        string? shadingHex,
        string? textColorHex = null);
}
