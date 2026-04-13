namespace PmTracker.Web.Models.ViewModels;

public sealed class PdfExportRequestViewModel
{
    public int ProjektId { get; set; }
    public int? JednaniId { get; set; }
}

public sealed class PdfExportTemplateViewModel
{
    public string ExportVariant { get; init; } = "project_all"; // project_all | meeting | task_single
    public bool AutoPrint { get; init; }
    public int ProjektId { get; init; }
    public string? ProjektZkratka { get; init; }
    public required string ProjektNazev { get; init; }
    public int? JednaniId { get; init; }
    public int? JednaniCislo { get; init; }
    public DateTime? JednaniDatum { get; init; }
    public string? JednaniMisto { get; init; }
    public required string JednaniStav { get; init; }
    public required string Vytvoril { get; init; }
    public DateTime VytvorenoDne { get; init; }
    public required string SnapshotSummary { get; init; }
    public string? PreparationSummary { get; init; }
    public IReadOnlyList<PdfAttendanceGroupViewModel> Dochazka { get; init; } = Array.Empty<PdfAttendanceGroupViewModel>();
    public IReadOnlyList<PdfRoleAssignmentViewModel> ProjektoveRole { get; init; } = Array.Empty<PdfRoleAssignmentViewModel>();
    public required IReadOnlyList<string> AppliedRuleSummary { get; init; }
    public required IReadOnlyList<PdfLegendItemViewModel> Legenda { get; init; }
    public required IReadOnlyList<PdfExportRecordViewModel> Zaznamy { get; init; }
    public string NormalizedVariant { get; init; } = "project_all";
    public bool IsMeeting { get; init; }
    public bool IsProjectSummary { get; init; }
    public string DocumentTitle { get; init; } = string.Empty;
    public string ExportTypeLabel { get; init; } = string.Empty;
    public IReadOnlyList<PdfExportSubsystemGroupViewModel> SubsystemGroups { get; init; } = Array.Empty<PdfExportSubsystemGroupViewModel>();
}

public sealed class PdfExportSubsystemGroupViewModel
{
    public required string Subsystem { get; init; }
    public required IReadOnlyList<PdfExportRecordViewModel> Records { get; init; }
}

public sealed class PdfRoleAssignmentViewModel
{
    public required string Osoba { get; init; }
    public required string TypRole { get; init; }
    public required string Role { get; init; }
    public string? Subsystem { get; init; }
}

public sealed class PdfAttendanceGroupViewModel
{
    public required string Stav { get; init; }
    public required IReadOnlyList<string> Osoby { get; init; }
}

public sealed class PdfLegendItemViewModel
{
    public required string Zkratka { get; init; }
    public required string Popis { get; init; }
}

public sealed class PdfExportRecordViewModel
{
    public int ZaznamId { get; init; }
    public int CisloZaznamu { get; init; }
    public required string CisloViditelne { get; init; }
    public int CisloViditelneA { get; init; }
    public int CisloViditelneB { get; init; }
    public required string Nazev { get; init; }
    public string? Cil { get; init; }
    public string? Popis { get; init; }
    public required string KategorieKod { get; init; }
    public required string Kategorie { get; init; }
    public string? TypUkoluKod { get; init; }
    public string? TypUkolu { get; init; }
    public required string Stav { get; init; }
    public bool IsPaused { get; init; }
    public required string Vlastnik { get; init; }
    public required string SubsystemKod { get; init; }
    public required string Subsystem { get; init; }
    public int SubsystemPoradi { get; init; }
    public bool SubsystemHasProjectOrder { get; init; }
    public DateTime DatumZalozeni { get; init; }
    public IReadOnlyList<DateTime> HistorieTerminu { get; init; } = Array.Empty<DateTime>();
    public DateTime? Termin { get; init; }
    public IReadOnlyList<string> ExterniVazby { get; init; } = Array.Empty<string>();
    public required IReadOnlyList<string> Spoluprace { get; init; }
    public required IReadOnlyList<PdfExportCommentViewModel> Vyjadreni { get; init; }
}

public sealed class PdfExportCommentViewModel
{
    public required string Autor { get; init; }
    public required DateTime Datum { get; init; }
    public required string Text { get; init; }
    public int Delka { get; init; }
    public int? JednaniCislo { get; init; }
    public DateTime? JednaniDatum { get; init; }
    public string? JednaniStav { get; init; }
    public bool IsNew { get; init; }
    public string? HighlightColor { get; init; }
}
