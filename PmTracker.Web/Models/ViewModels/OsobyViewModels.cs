namespace PmTracker.Web.Models.ViewModels;

public sealed class OsobyIndexViewModel : BaseViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackUrl { get; set; }
    public string? BackLabel { get; set; }
    public required IReadOnlyList<OsobaListItemViewModel> Osoby { get; init; }
    public IReadOnlyList<LookupOptionViewModel> Organizace { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> OrganizacniCelky { get; init; } = Array.Empty<LookupOptionViewModel>();
    public bool CanManagePeople { get; set; }
}

public sealed class OsobaListItemViewModel
{
    public int Id { get; init; }
    public required string Jmeno { get; init; }
    public required string Prijmeni { get; init; }
    public string? Titul { get; init; }
    public required string Email { get; init; }
    public string? OrganizaceKod { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelekKod { get; init; }
    public string? OrganizacniCelek { get; init; }
    public bool JeAdUcet { get; init; }
    public bool LocationLocked { get; init; }
}
