using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Export;

public interface IExportQueries
{
    bool ProjektExists(int projektId);
    int ResolveMeetingProjectId(int jednaniId);
    PdfExportTemplateViewModel BuildProjectPrintTemplate(int projektId, CurrentUserContextViewModel currentUser, bool autoPrint);
    PdfExportTemplateViewModel BuildMeetingPrintTemplate(int jednaniId, CurrentUserContextViewModel currentUser, bool autoPrint);
    PdfExportTemplateViewModel BuildTaskPrintTemplate(int projektId, int zaznamId, CurrentUserContextViewModel currentUser, bool autoPrint);
}
