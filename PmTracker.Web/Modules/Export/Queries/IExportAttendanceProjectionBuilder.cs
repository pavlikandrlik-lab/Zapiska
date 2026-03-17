using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Export.Queries;

public interface IExportAttendanceProjectionBuilder
{
    List<PdfAttendanceGroupViewModel> BuildAttendanceGroups(int meetingId, int projectId);
}
