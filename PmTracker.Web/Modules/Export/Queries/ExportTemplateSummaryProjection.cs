using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Export.Queries;

public sealed record class ExportTemplateSummaryProjection
{
    public required string JednaniStav { get; init; }
    public required string SnapshotSummary { get; init; }
    public string? PreparationSummary { get; init; }
    public required IReadOnlyList<string> AppliedRuleSummary { get; init; }
    public required IReadOnlyList<PdfLegendItemViewModel> Legenda { get; init; }
}
