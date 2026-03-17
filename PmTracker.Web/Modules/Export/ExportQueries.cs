using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Export.Queries;

namespace PmTracker.Web.Modules.Export;

public sealed class ExportQueries(
    IExportProjektExistsQueryHandler exportProjektExistsQueryHandler,
    IExportMeetingProjectIdQueryHandler exportMeetingProjectIdQueryHandler,
    IExportTemplateUseCase exportTemplateUseCase) : IExportQueries
{
    public bool ProjektExists(int projektId)
        => exportProjektExistsQueryHandler.Handle(projektId);

    public int ResolveMeetingProjectId(int jednaniId)
        => exportMeetingProjectIdQueryHandler.Handle(jednaniId);

    public PdfExportTemplateViewModel BuildProjectPrintTemplate(int projektId, CurrentUserContextViewModel currentUser, bool autoPrint)
        => exportTemplateUseCase.BuildProjectTemplate(projektId, currentUser, autoPrint);

    public PdfExportTemplateViewModel BuildMeetingPrintTemplate(int jednaniId, CurrentUserContextViewModel currentUser, bool autoPrint)
        => exportTemplateUseCase.BuildMeetingTemplate(jednaniId, currentUser, autoPrint);

    public PdfExportTemplateViewModel BuildTaskPrintTemplate(int projektId, int zaznamId, CurrentUserContextViewModel currentUser, bool autoPrint)
        => exportTemplateUseCase.BuildTaskTemplate(projektId, zaznamId, currentUser, autoPrint);
}
