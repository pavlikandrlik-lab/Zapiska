using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

public interface IWordExportHeaderSectionWriter
{
    void Append(Body body, PdfExportTemplateViewModel model, string documentTitle);
}
