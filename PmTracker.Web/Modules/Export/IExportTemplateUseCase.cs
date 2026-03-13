using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Export;

public interface IExportTemplateUseCase
{
    PdfExportTemplateViewModel BuildProjectTemplate(int projektId, CurrentUserContextViewModel currentUser, bool autoPrint);
    PdfExportTemplateViewModel BuildMeetingTemplate(int jednaniId, CurrentUserContextViewModel currentUser, bool autoPrint);
    PdfExportTemplateViewModel BuildTaskTemplate(int projektId, int zaznamId, CurrentUserContextViewModel currentUser, bool autoPrint);
}
