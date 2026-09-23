namespace PmTracker.Web.Models.ViewModels;

public sealed class ProfilPageViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackUrl { get; set; }
    public string? BackLabel { get; set; }
    public required CurrentUserContextViewModel Uzivatel { get; init; }
    /// <summary>A5 (2026-07-08): karty per INSTANCE role (Typ · Role · Projekt · Subsystém).
    /// Nahrazuje dřívější dvojici MojeRole (jen aplikační) + OdvozenaPrava (plochá tabulka grantů).</summary>
    public required IReadOnlyList<ProfilRoleInstanceViewModel> MojeRole { get; init; }
}

/// <summary>Jedna instance role uživatele: aplikační, projektová (role × projekt),
/// nebo subsystémová (role × projekt × subsystém). Rozklikávací karta v profilu.</summary>
public sealed class ProfilRoleInstanceViewModel
{
    /// <summary>"Aplikační" | "Projektová" | "Subsystémová".</summary>
    public required string TypRole { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public string? Popis { get; init; }
    public string? ProjektZkratka { get; init; }
    public string? ProjektNazev { get; init; }
    public string? SubsystemKod { get; init; }
    public string? SubsystemNazev { get; init; }
    public required IReadOnlyList<ProfilRoleAkceViewModel> Akce { get; init; }
}

public sealed class ProfilRoleAkceViewModel
{
    public required string PermissionKlic { get; init; }
    public required string PermissionNazev { get; init; }
    public bool IsAllowed { get; init; }
    public required string ScopeSummary { get; init; }
}
