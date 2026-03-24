using System;

namespace PmTracker.Web.Models.ViewModels;

public sealed class CiselnikyIndexViewModel
{
    public required IReadOnlyList<CiselnikListItemViewModel> Ciselniky { get; init; }
}

public sealed class CiselnikyDashboardViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackUrl { get; set; }
    public string? BackLabel { get; set; }
    public required IReadOnlyList<CiselnikListItemViewModel> Ciselniky { get; init; }
    public required CiselnikDetailViewModel VybranyCiselnik { get; init; }
}

public sealed class CiselnikListItemViewModel
{
    public required string Key { get; init; }
    public required string Nazev { get; init; }
    public int PocetPolozek { get; init; }
}

public sealed class CiselnikDetailViewModel : BaseViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackUrl { get; set; }
    public string? BackLabel { get; set; }
    public required string Key { get; init; }
    public required string Nazev { get; init; }
    public bool CanCreate { get; init; } = true;
    public bool CanChangeLockState { get; init; }
    public required IReadOnlyList<string> SloupceNavic { get; init; }
    public IReadOnlyList<LookupOptionViewModel> HodnotaNavicVolby { get; init; } = Array.Empty<LookupOptionViewModel>();
    public bool IsHodnotaNavicSelect { get; init; }
    public bool IsHodnotaNavicRequired { get; init; }
    public required IReadOnlyList<CiselnikRadekViewModel> Polozky { get; init; }
    public bool CanEditCiselnik { get; set; }
    public bool IsArchitect { get; set; }

    public bool IsSubsystemCiselnik => string.Equals(Key, "subsystemy", StringComparison.OrdinalIgnoreCase);

    public bool IsHarmonogramCiselnik => string.Equals(Key, "harmonogram-kroky", StringComparison.OrdinalIgnoreCase);

    public bool CanEditThisCiselnik => CanEditCiselnik && (!IsHarmonogramCiselnik || IsArchitect);
}

public sealed class CiselnikRadekViewModel
{
    public int Id { get; init; }
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public bool IsLocked { get; init; }
    public bool CanChangeLockState { get; init; }
    public bool CanEdit { get; init; } = true;
    public bool CanDelete { get; init; } = true;
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
