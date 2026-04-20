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
            AppendSectionProperties(body);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    // ── private record structs (používány napříč partials, musí být v core) ──

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

    // ── source-generated regexes (musí být v partial class, ne v static helper) ──

    [GeneratedRegex(@"^rgba?\(\s*(?<r>\d{1,3})\s*,\s*(?<g>\d{1,3})\s*,\s*(?<b>\d{1,3})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RgbRegex();

    [GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakTagRegex();

    [GeneratedRegex(@"(?:^|\s)ql-indent-(?<level>\d+)(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex IndentClassRegex();
}
