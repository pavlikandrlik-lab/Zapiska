using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

public interface IWordExportService
{
    byte[] BuildDocument(PdfExportTemplateViewModel model);
}
