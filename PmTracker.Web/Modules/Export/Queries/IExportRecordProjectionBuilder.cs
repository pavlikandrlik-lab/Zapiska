using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Export.Queries;

public interface IExportRecordProjectionBuilder
{
    List<PdfExportRecordViewModel> BuildExportRecords(
        int projectId,
        int? anchorMeetingId,
        int? specificRecordId,
        bool limitComments,
        bool applyMeetingSnapshotRules);
}
