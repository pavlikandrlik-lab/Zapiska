namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjektJednaniTabViewModel
{
    public int ProjektId { get; init; }
    public IReadOnlyList<JednaniListItemViewModel> Jednani { get; init; } = Array.Empty<JednaniListItemViewModel>();
    public IReadOnlyList<JednaniYearGroupViewModel> RocniSkupiny { get; init; } = Array.Empty<JednaniYearGroupViewModel>();
    public int? PreviewRok { get; set; }
    public IReadOnlyList<LookupOptionViewModel> StavyJednani { get; init; } = Array.Empty<LookupOptionViewModel>();
    public bool CanCreateMeetings { get; set; }
    public bool CanEditMeetings { get; set; }
    public string? ReturnToProjectUrl { get; set; }
}

public sealed class JednaniListItemViewModel
{
    public int Id { get; init; }
    public int CisloJednani { get; init; }
    public DateTime Datum { get; init; }
    public TimeOnly CasZacatek { get; init; }
    public required string Misto { get; init; }
    public string? StavKod { get; init; }
    public required string Stav { get; init; }
    public string? UzamklOsoba { get; init; }
}

public sealed class JednaniOptionViewModel
{
    public int Id { get; init; }
    public required string Label { get; init; }
    public DateTime Datum { get; init; }
    public string? StavKod { get; init; }

    public bool IsDraft => string.Equals(StavKod, "DRAFT", StringComparison.OrdinalIgnoreCase);
}
