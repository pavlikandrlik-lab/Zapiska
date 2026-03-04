namespace PmTracker.Web.Models.ViewModels;

public sealed class ProfilPageViewModel
{
    public required CurrentUserContextViewModel Uzivatel { get; init; }
    public required IReadOnlyList<ProfilRolePravaViewModel> MojeRole { get; init; }
    public required IReadOnlyList<ProfilOdvozenePravoViewModel> OdvozenaPrava { get; init; }
}

public sealed class ProfilRolePravaViewModel
{
    public int RoleId { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public string? Popis { get; init; }
    public required IReadOnlyList<ProfilRoleAkceViewModel> Akce { get; init; }
}

public sealed class ProfilRoleAkceViewModel
{
    public required string PermissionKlic { get; init; }
    public required string PermissionNazev { get; init; }
    public bool IsAllowed { get; init; }
    public required string ScopeSummary { get; init; }
}

public sealed class ProfilOdvozenePravoViewModel
{
    public required string PermissionKlic { get; init; }
    public required string PermissionNazev { get; init; }
    public bool IsAllowed { get; init; }
    public required string ScopeSummary { get; init; }
    public required string SourceSummary { get; init; }
}
