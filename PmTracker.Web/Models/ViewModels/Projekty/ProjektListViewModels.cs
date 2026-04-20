namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjektyIndexViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackUrl { get; set; }
    public string? BackLabel { get; set; }
    public required IReadOnlyList<ProjektListItemViewModel> Projekty { get; init; }
    public IReadOnlyList<LookupOptionViewModel> StavyProjektu { get; init; } = Array.Empty<LookupOptionViewModel>();
    public bool IsAdmin { get; init; }
    public bool CanCreate { get; init; }
}

public sealed class ProjektListItemViewModel
{
    public int Id { get; init; }
    public required string Zkratka { get; init; }
    public required string Nazev { get; init; }
    public string? StavKod { get; init; }
    public required string Stav { get; init; }
    public bool PouzivatIdentJednani { get; init; }
    public string? MistoPlneni { get; init; }
    public string? CisloRamcoveSmlouvy { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
}

public sealed class ProjektFiltryViewModel
{
    public IReadOnlyList<string> Subsystemy { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LookupOptionViewModel> SubsystemyMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<string> Kategorie { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LookupOptionViewModel> KategorieMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<string> StavyUkolu { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LookupOptionViewModel> StavyUkoluMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<string> TypyUkolu { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LookupOptionViewModel> TypyUkoluMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<string> Vlastnici { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LookupOptionViewModel> VlastniciMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> StavyJednaniVyjadreni { get; init; } = Array.Empty<LookupOptionViewModel>();
}
