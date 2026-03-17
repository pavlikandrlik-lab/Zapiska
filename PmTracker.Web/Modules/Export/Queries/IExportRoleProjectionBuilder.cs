using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Export.Queries;

public interface IExportRoleProjectionBuilder
{
    List<PdfRoleAssignmentViewModel> BuildProjectRoleRows(int projectId);
}
