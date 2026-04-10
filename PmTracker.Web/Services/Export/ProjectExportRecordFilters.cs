namespace PmTracker.Web.Services.Export;

public sealed record ProjectExportRecordFilters
{
    public bool UseCurrentFilters { get; init; }
    public string? Subsystem { get; init; }
    public string? Kategorie { get; init; }
    public string? Stav { get; init; }
    public string? Typ { get; init; }
    public int? VlastnikId { get; init; }
    public bool Aktivni { get; init; }
    public bool Mine { get; init; }
    public int? JednaniVyjadreniStavId { get; init; }

    public bool HasRelevantFilters =>
        !string.IsNullOrWhiteSpace(Subsystem)
        || !string.IsNullOrWhiteSpace(Kategorie)
        || !string.IsNullOrWhiteSpace(Stav)
        || !string.IsNullOrWhiteSpace(Typ)
        || VlastnikId.HasValue
        || Aktivni
        || Mine
        || JednaniVyjadreniStavId.HasValue;
}
