using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

public interface IWordExportRecordsSectionWriter
{
    void Append(Body body, MainDocumentPart mainPart, IReadOnlyList<PdfExportRecordViewModel> records);
}
