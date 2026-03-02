namespace PmTracker.Web.Models.ViewModels;

public sealed class NastaveniDashboardViewModel
{
    public required IReadOnlyList<NastaveniSectionItemViewModel> Sekce { get; init; }
    public required NastaveniPanelViewModel AktivniPanel { get; init; }
    public int? SelectedUserId { get; init; }
    public int? SelectedProjektId { get; init; }
}

public sealed class NastaveniSectionItemViewModel
{
    public required string Key { get; init; }
    public required string Nazev { get; init; }
    public required string Popis { get; init; }
    public int Pocet { get; init; }
}

public sealed class NastaveniPanelViewModel
{
    public required string SectionKey { get; init; }
    public required string Nazev { get; init; }
    public required string Popis { get; init; }
    public required IReadOnlyList<RoleViewModel> Role { get; init; }
    public required IReadOnlyList<PermissionCategoryViewModel> PermissionCategories { get; init; }
    public required IReadOnlyList<PermissionViewModel> Permissions { get; init; }
    public required IReadOnlyList<RolePermissionScopeViewModel> RolePermissionScopes { get; init; }
    public required IReadOnlyList<UserRoleAssignmentViewModel> UserRoles { get; init; }
    public required EffectivePermissionsPreviewViewModel EffectivePermissions { get; init; }
    public required IReadOnlyList<NastaveniProjektItemViewModel> Projekty { get; init; }
}

public sealed class PermissionCategoryViewModel
{
    public int Id { get; init; }
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public int SortOrder { get; init; }
}

public sealed class PermissionViewModel
{
    public int Id { get; init; }
    public required string Klic { get; init; }
    public required string Nazev { get; init; }
    public required string CategoryKod { get; init; }
    public required string ScopeLevel { get; init; } // GLOBAL|PROJECT
    public bool IsSystem { get; init; }
    public bool IsActive { get; init; }
}

public sealed class RoleViewModel
{
    public int Id { get; init; }
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public required string Popis { get; init; }
    public bool IsSystem { get; init; }
    public bool IsActive { get; init; }
}

public sealed class RolePermissionScopeViewModel
{
    public int Id { get; init; }
    public int RoleId { get; init; }
    public required string RoleKod { get; init; }
    public int PermissionId { get; init; }
    public required string PermissionKlic { get; init; }
    public required string ScopeMode { get; init; } // ALL|INCLUDE
    public bool IsAllowed { get; init; }
    public required IReadOnlyList<int> ProjektIds { get; init; }
}

public sealed class UserRoleAssignmentViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public required string Email { get; init; }
    public required IReadOnlyList<string> RoleKody { get; init; }
    public required IReadOnlyList<UserRoleItemViewModel> RoleAssignments { get; init; }
}

public sealed class UserRoleItemViewModel
{
    public int RoleId { get; init; }
    public required string RoleKod { get; init; }
}

public sealed class EffectivePermissionsPreviewViewModel
{
    public int SelectedUserId { get; init; }
    public int? SelectedProjectId { get; init; } // null = ALL
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public int? ProjektId { get; init; }
    public required string ProjektNazev { get; init; }
    public required IReadOnlyList<EffectiveRightsFilterOptionViewModel> AvailableUsers { get; init; }
    public required IReadOnlyList<EffectiveRightsFilterOptionViewModel> AvailableProjects { get; init; }
    public required IReadOnlyList<EffectivePermissionRowViewModel> Rows { get; init; }
}

public sealed class EffectivePermissionRowViewModel
{
    public required string PermissionKlic { get; init; }
    public required string PermissionNazev { get; init; }
    public bool IsAllowed { get; init; }
    public required string ScopeSummary { get; init; }
}

public sealed class NastaveniProjektItemViewModel
{
    public int Id { get; init; }
    public required string Nazev { get; init; }
}

public sealed class EffectiveRightsFilterOptionViewModel
{
    public int Id { get; init; }
    public required string Label { get; init; }
}

public sealed class AuthzRoleModalViewModel
{
    public required SaveAuthzRoleCommand Command { get; init; }
    public int? UserId { get; init; }
    public int? ProjektId { get; init; }
    public bool IsEdit { get; init; }
}

public sealed class AuthzPermissionModalViewModel
{
    public required SaveAuthzPermissionCommand Command { get; init; }
    public required IReadOnlyList<PermissionCategoryViewModel> Categories { get; init; }
    public required IReadOnlyList<LookupOptionViewModel> AvailableKeys { get; init; }
    public required IReadOnlyList<PermissionCatalogEntryViewModel> PermissionCatalog { get; init; }
    public int? UserId { get; init; }
    public int? ProjektId { get; init; }
    public bool IsEdit { get; init; }
}

public sealed class UserRolesModalViewModel
{
    public required SaveUserRolesForUserCommand Command { get; init; }
    public required string OsobaLabel { get; init; }
    public required string Email { get; init; }
    public required IReadOnlyList<LookupOptionViewModel> RoleOptions { get; init; }
    public int? UserId { get; init; }
    public int? ProjektId { get; init; }
}

public sealed class RolePermissionModalViewModel
{
    public required SaveRolePermissionCommand Command { get; init; }
    public required IReadOnlyList<LookupOptionViewModel> RoleOptions { get; init; }
    public required IReadOnlyList<LookupOptionViewModel> PermissionOptions { get; init; }
    public required IReadOnlyList<LookupOptionViewModel> ProjectOptions { get; init; }
    public int? UserId { get; init; }
    public int? ProjektId { get; init; }
    public bool IsEdit { get; init; }
}
