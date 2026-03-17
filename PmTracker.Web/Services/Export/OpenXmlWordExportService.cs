using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

public sealed class OpenXmlWordExportService : IWordExportService
{
    private readonly IWordExportHeaderSectionWriter _headerSectionWriter;
    private readonly IWordExportRecordsSectionWriter _recordsSectionWriter;

    public OpenXmlWordExportService(
        IWordExportHeaderSectionWriter headerSectionWriter,
        IWordExportRecordsSectionWriter recordsSectionWriter)
    {
        _headerSectionWriter = headerSectionWriter;
        _recordsSectionWriter = recordsSectionWriter;
    }

    public byte[] BuildDocument(PdfExportTemplateViewModel model)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body ?? throw new InvalidOperationException("Word body nebyl inicializován.");

            _headerSectionWriter.Append(body, model, BuildDocumentTitle(model));
            _recordsSectionWriter.Append(body, mainPart, model.Zaznamy);
            AppendSectionProperties(body);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static string BuildDocumentTitle(PdfExportTemplateViewModel model)
    {
        var variant = (model.ExportVariant ?? "project_all").Trim().ToLowerInvariant();
        return variant switch
        {
            "meeting" => $"Zápis z jednání projektu {model.ProjektNazev} číslo {model.JednaniCislo}",
            "task_single" => $"Zápis úkolu projektu {model.ProjektNazev}",
            _ => $"Souhrnný zápis projektu {model.ProjektNazev}"
        };
    }

    private static void AppendSectionProperties(Body body)
    {
        if (body.Elements<SectionProperties>().Any())
        {
            return;
        }

        body.Append(new SectionProperties(
            new PageSize
            {
                Width = 11906U,
                Height = 16838U,
                Orient = PageOrientationValues.Portrait
            },
            new PageMargin
            {
                Top = 720,
                Right = 720U,
                Bottom = 720,
                Left = 720U,
                Header = 420U,
                Footer = 420U,
                Gutter = 0U
            }));
    }
}
