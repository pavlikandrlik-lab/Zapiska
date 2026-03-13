using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Export;

public sealed record class ExportTemplateQueryResult
{
    public required string ExportVariant { get; init; }
    public int ProjektId { get; init; }
    public string? ProjektZkratka { get; init; }
    public required string ProjektNazev { get; init; }
    public int? JednaniId { get; init; }
    public int? JednaniCislo { get; init; }
    public DateTime? JednaniDatum { get; init; }
    public string? JednaniMisto { get; init; }
    public required string JednaniStav { get; init; }
    public required string SnapshotSummary { get; init; }
    public string? PreparationSummary { get; init; }
    public IReadOnlyList<PdfAttendanceGroupViewModel> Dochazka { get; init; } = [];
    public IReadOnlyList<PdfRoleAssignmentViewModel> ProjektoveRole { get; init; } = [];
    public required IReadOnlyList<string> AppliedRuleSummary { get; init; }
    public required IReadOnlyList<PdfLegendItemViewModel> Legenda { get; init; }
    public required IReadOnlyList<PdfExportRecordViewModel> Zaznamy { get; init; }
}
