namespace PmTracker.Web.Modules.Export.Queries;

public interface IExportTemplateSummaryBuilder
{
    ExportTemplateSummaryProjection BuildProjectSummary();
    ExportTemplateSummaryProjection BuildMeetingSummary(string? meetingStatusName);
    ExportTemplateSummaryProjection BuildTaskSummary();
}
