using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Export;

/// <summary>Word export service kontrakt.</summary>
public interface IWordExportService
{
    byte[] BuildDocument(PdfExportTemplateViewModel model);
}

/// <summary>
/// Orchestrátor Word exportu. Deleguje na partial soubory:
/// <list type="bullet">
///   <item><description>Metadata.cs — cover page, hlavička projektu, docházka</description></item>
///   <item><description>Records.cs — tabulka záznamů, komentáře, termíny, HTML parser</description></item>
/// </list>
/// Sdílený helper: <see cref="OpenXmlWordElements"/>.
/// </summary>
public sealed partial class OpenXmlWordExportService(IRichTextContentService richTextContentService) : IWordExportService
{
    private const string PausedRecordFillHex = "FDF4E8";

    /// <summary>Ukončený úkol — jemná modrá, stejná jako v tiskovém CSS (2026-09-05).</summary>
    private const string CompletedRecordFillHex = "EFF6FF";

    /// <summary>Sestaví Word dokument ze šablony exportu.</summary>
    public byte[] BuildDocument(PdfExportTemplateViewModel model)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body ?? throw new InvalidOperationException("Word body nebyl inicializován.");

            AppendHeader(body, model, BuildDocumentTitle(model));
            AppendRecordsSection(body, mainPart, model.Zaznamy);
            AppendSectionProperties(body, mainPart);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    // 2026-09-08: HtmlInlineToken/HtmlParagraphModel/HtmlStyleState se přestěhovaly
    // do RichTextHtmlParser jako RichTextToken/RichTextParagraph — parser sdílí i výzva.

    // ── source-generated regexes (musí být v partial class, ne v static helper) ──

    [GeneratedRegex(@"^rgba?\(\s*(?<r>\d{1,3})\s*,\s*(?<g>\d{1,3})\s*,\s*(?<b>\d{1,3})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RgbRegex();
}
