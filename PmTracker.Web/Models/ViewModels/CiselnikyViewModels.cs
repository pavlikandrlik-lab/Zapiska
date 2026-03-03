namespace PmTracker.Web.Models.ViewModels;

public sealed class CiselnikyIndexViewModel
{
    public required IReadOnlyList<CiselnikListItemViewModel> Ciselniky { get; init; }
}

public sealed class CiselnikyDashboardViewModel
{
    public required IReadOnlyList<CiselnikListItemViewModel> Ciselniky { get; init; }
    public required CiselnikDetailViewModel VybranyCiselnik { get; init; }
}

public sealed class CiselnikListItemViewModel
{
    public required string Key { get; init; }
    public required string Nazev { get; init; }
    public int PocetPolozek { get; init; }
}

public sealed class CiselnikDetailViewModel
{
    public required string Key { get; init; }
    public required string Nazev { get; init; }
    public bool CanCreate { get; init; } = true;
    public bool CanChangeLockState { get; init; }
    public required IReadOnlyList<string> SloupceNavic { get; init; }
    public IReadOnlyList<LookupOptionViewModel> HodnotaNavicVolby { get; init; } = Array.Empty<LookupOptionViewModel>();
    public bool IsHodnotaNavicSelect { get; init; }
    public bool IsHodnotaNavicRequired { get; init; }
    public required IReadOnlyList<CiselnikRadekViewModel> Polozky { get; init; }
}

public sealed class CiselnikRadekViewModel
{
    public int Id { get; init; }
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public bool IsLocked { get; init; }
    public bool CanEdit { get; init; } = true;
    public bool CanDelete { get; init; } = true;
    public bool CanChangeLockState { get; init; }
    public required IReadOnlyList<string> HodnotyNavic { get; init; }
    public IReadOnlyList<string> HodnotyNavicRaw { get; init; } = Array.Empty<string>();
}

public sealed class CiselnikRadekEditViewModel
{
    public required string Key { get; init; }
    public required string CiselnikNazev { get; init; }
    public int Id { get; init; }
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public bool IsLocked { get; init; }
    public bool CanChangeLockState { get; init; }
    public string? SloupecNavic { get; init; }
    public string? HodnotaNavic { get; init; }
    public string? HodnotaNavicRaw { get; init; }
    public IReadOnlyList<LookupOptionViewModel> HodnotaNavicVolby { get; init; } = Array.Empty<LookupOptionViewModel>();
    public bool IsHodnotaNavicSelect { get; init; }
    public bool IsHodnotaNavicRequired { get; init; }
}
