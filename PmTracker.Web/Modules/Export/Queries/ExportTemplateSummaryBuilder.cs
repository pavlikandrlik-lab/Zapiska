namespace PmTracker.Web.Modules.Export.Queries;

public sealed class ExportTemplateSummaryBuilder : IExportTemplateSummaryBuilder
{
    public ExportTemplateSummaryProjection BuildProjectSummary()
    {
        return new ExportTemplateSummaryProjection
        {
            JednaniStav = "Projekt",
            SnapshotSummary = "Tisk kompletního projektu bez filtru.",
            PreparationSummary = null,
            AppliedRuleSummary = ["Bez omezení"],
            Legenda = []
        };
    }

    public ExportTemplateSummaryProjection BuildMeetingSummary(string? meetingStatusName)
    {
        return new ExportTemplateSummaryProjection
        {
            JednaniStav = string.IsNullOrWhiteSpace(meetingStatusName) ? "-" : meetingStatusName.Trim(),
            SnapshotSummary = string.Empty,
            PreparationSummary = null,
            AppliedRuleSummary = ["Automatický meeting výstup"],
            Legenda = []
        };
    }

    public ExportTemplateSummaryProjection BuildTaskSummary()
    {
        return new ExportTemplateSummaryProjection
        {
            JednaniStav = "Úkol",
            SnapshotSummary = "Tisk jednoho úkolu.",
            PreparationSummary = null,
            AppliedRuleSummary = ["Automatický task výstup"],
            Legenda = []
        };
    }
}
