using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Modules.Settings;

public sealed class SettingsAuthzQueries(
    PmTrackerDbContext dbContext,
    IUserAuthorizationSnapshotBuilder userAuthorizationSnapshotBuilder,
    IPersonIdentityMatcher personIdentityMatcher,
    ITextNormalizer textNormalizer) : ISettingsAuthzQueries
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public NastaveniDashboardViewModel BuildNastaveniDashboard(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId)
    {
        var panel = BuildNastaveniPanel(section, currentUser, userId, projektId);

        return new NastaveniDashboardViewModel
        {
            Sekce = BuildNastaveniSections(currentUser, panel),
            AktivniPanel = panel,
            SelectedUserId = panel.EffectivePermissions.SelectedUserId,
            SelectedProjektId = panel.EffectivePermissions.SelectedProjectId
        };
    }

    public NastaveniPanelViewModel BuildNastaveniPanel(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId)
    {
        var canManage = currentUser.HasPermission(PermissionKeys.SettingsManage);
        var normalized = NormalizeSettingsSection(section, canManage);

        var categories = dbContext.AuthzPermissionCategories.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Kod)
            .Select(x => new PermissionCategoryViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                SortOrder = x.SortOrder
            })
            .ToList();

        var permissionRows = dbContext.AuthzPermissions.AsNoTracking()
            .OrderBy(x => x.Klic)
            .ToList();
        var permissions = permissionRows
            .Select(x => new PermissionViewModel
            {
                Id = x.Id,
                Klic = x.Klic,
                Nazev = x.Nazev,
                CategoryKod = categories.FirstOrDefault(category => category.Id == x.CategoryId)?.Kod ?? "-",
                ScopeLevel = x.ScopeLevel,
                IsActive = x.IsActive,
                IsSystem = x.IsSystem
            })
            .ToList();

        var roles = dbContext.AuthzRoles.AsNoTracking()
            .OrderBy(x => x.Kod)
            .Select(x => new RoleViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                Popis = x.Popis ?? string.Empty,
                IsSystem = x.IsSystem,
                IsActive = x.IsActive
            })
            .ToList();

        var rolePermissionProjects = dbContext.AuthzRolePermissionProjects.AsNoTracking().ToList();

        var rolePermissionScopes = dbContext.AuthzRolePermissions.AsNoTracking()
            .OrderBy(x => x.RoleId)
            .ThenBy(x => x.PermissionId)
            .ToList()
            .Select(x => new RolePermissionScopeViewModel
            {
                Id = x.Id,
                RoleId = x.RoleId,
                RoleKod = roles.FirstOrDefault(role => role.Id == x.RoleId)?.Kod ?? "-",
                PermissionId = x.PermissionId,
                PermissionKlic = permissions.FirstOrDefault(permission => permission.Id == x.PermissionId)?.Klic ?? "-",
                ScopeMode = x.ScopeMode,
                IsAllowed = x.IsAllowed,
                ProjektIds = rolePermissionProjects
                    .Where(project => project.RolePermissionId == x.Id)
                    .Select(project => project.ProjektId)
                    .Distinct()
                    .ToList()
            })
            .ToList();

        var users = dbContext.Osoby.AsNoTracking()
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .Select(x => new { x.Id, x.Titul, x.Jmeno, x.Prijmeni, x.Email })
            .ToList();

        var userRoleRows = dbContext.AuthzUserRoles.AsNoTracking()
            .Where(x => x.IsActive)
            .Join(
                dbContext.AuthzRoles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new
                {
                    userRole.OsobaId,
                    userRole.RoleId,
                    RoleKod = role.Kod,
                    RoleIsActive = role.IsActive
                })
            .Where(x => x.RoleIsActive)
            .ToList();

        var userRoleByOsobaId = userRoleRows
            .GroupBy(x => x.OsobaId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(x => x.RoleKod, StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => new UserRoleItemViewModel
                    {
                        RoleId = x.RoleId,
                        RoleKod = x.RoleKod
                    })
                    .ToList());

        var userRoleAssignments = users.Select(user =>
        {
            var userRoles = userRoleByOsobaId.TryGetValue(user.Id, out var assignedRoles)
                ? assignedRoles
                : [];

            return new UserRoleAssignmentViewModel
            {
                OsobaId = user.Id,
                Osoba = BuildDisplayName(user.Titul, user.Jmeno, user.Prijmeni, user.Id),
                Email = user.Email?.Trim() ?? string.Empty,
                RoleKody = userRoles.Select(x => x.RoleKod).ToList(),
                RoleAssignments = userRoles
            };
        }).ToList();

        var projects = dbContext.Projekty.AsNoTracking()
            .OrderBy(x => x.Zkratka)
            .Select(x => new NastaveniProjektItemViewModel { Id = x.Id, Nazev = x.CelyNazev })
            .ToList();

        var (title, description) = normalized switch
        {
            "akce" => ("Akce", "Katalog akcí aplikace."),
            "role-akce" => ("Mapování rolí na akce", "Nastavení oprávnění a rozsahů ALL / INCLUDE."),
            "uzivatele-role" => ("Přiřazení rolí uživatelům", "Mapování rolí na osoby."),
            "efektivni-prava" => ("Kontrola efektivních práv", "Diagnostický pohled na finální práva uživatele."),
            _ => ("Role", "Správa rolí administrátorů.")
        };

        return new NastaveniPanelViewModel
        {
            SectionKey = normalized,
            Nazev = title,
            Popis = description,
            Role = roles,
            PermissionCategories = categories,
            Permissions = permissions,
            RolePermissionScopes = rolePermissionScopes,
            UserRoles = userRoleAssignments,
            EffectivePermissions = BuildEffectivePermissionPreview(currentUser, userId, projektId),
            Projekty = projects
        };
    }

    private List<NastaveniSectionItemViewModel> BuildNastaveniSections(CurrentUserContextViewModel currentUser, NastaveniPanelViewModel panel)
    {
        var sections = new List<NastaveniSectionItemViewModel>
        {
            new() { Key = "role", Nazev = "Role", Popis = "Správa rolí", Pocet = panel.Role.Count },
            new() { Key = "akce", Nazev = "Akce", Popis = "Katalog akcí", Pocet = panel.Permissions.Count },
            new() { Key = "role-akce", Nazev = "Role -> Akce", Popis = "Mapování role/akce", Pocet = panel.RolePermissionScopes.Count },
            new() { Key = "uzivatele-role", Nazev = "Uživatelé -> Role", Popis = "Přiřazení rolí", Pocet = panel.UserRoles.Count }
        };

        if (currentUser.HasPermission(PermissionKeys.SettingsManage))
        {
            sections.Add(new NastaveniSectionItemViewModel
            {
                Key = "efektivni-prava",
                Nazev = "Efektivní práva",
                Popis = "Kontrola výsledných práv",
                Pocet = panel.EffectivePermissions.Rows.Count
            });
        }

        return sections;
    }

    private static string NormalizeSettingsSection(string? section, bool canManageSettings)
    {
        var normalized = (section ?? "role").Trim().ToLowerInvariant();
        if (normalized == "efektivni-prava" && !canManageSettings)
        {
            return "role";
        }

        return normalized is "role" or "akce" or "role-akce" or "uzivatele-role" or "efektivni-prava"
            ? normalized
            : "role";
    }

    private EffectivePermissionsPreviewViewModel BuildEffectivePermissionPreview(CurrentUserContextViewModel currentUser, int? selectedUserId, int? selectedProjectId)
    {
        var users = dbContext.Osoby.AsNoTracking()
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .ToList();
        var projects = dbContext.Projekty.AsNoTracking()
            .OrderBy(x => x.Zkratka)
            .ToList();

        var userId = selectedUserId ?? currentUser.OsobaId;
        var selectedUser = users.FirstOrDefault(x => x.Id == userId) ?? users.FirstOrDefault();
        if (selectedUser is null)
        {
            return new EffectivePermissionsPreviewViewModel
            {
                SelectedUserId = currentUser.OsobaId,
                SelectedProjectId = selectedProjectId,
                OsobaId = currentUser.OsobaId,
                Osoba = currentUser.DisplayName,
                ProjektId = selectedProjectId,
                ProjektNazev = selectedProjectId.HasValue
                    ? projects.FirstOrDefault(x => x.Id == selectedProjectId.Value)?.CelyNazev ?? "-"
                    : "Všechny projekty",
                AvailableUsers = [],
                AvailableProjects = [],
                Rows = []
            };
        }

        var authzSnapshot = userAuthorizationSnapshotBuilder.Build(selectedUser.Id);
        var previewContext = new CurrentUserContextViewModel
        {
            OsobaId = selectedUser.Id,
            Jmeno = selectedUser.Jmeno,
            Prijmeni = selectedUser.Prijmeni,
            DisplayName = BuildDisplayName(selectedUser.Titul, selectedUser.Jmeno, selectedUser.Prijmeni, selectedUser.Id),
            Email = selectedUser.Email?.Trim() ?? string.Empty,
            OrganizacniCelekKod = null,
            OrganizacniCelek = "-",
            IsSuperAdmin = authzSnapshot.IsSuperAdmin,
            RoleKody = authzSnapshot.RoleKody,
            VisibleProjectIds = authzSnapshot.VisibleProjectIds,
            DeletedProjectIds = authzSnapshot.DeletedProjectIds,
            PermissionGrants = authzSnapshot.PermissionGrants
        };

        var permissions = dbContext.AuthzPermissions.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Klic)
            .ToList();
        var projectCodesById = projects.ToDictionary(x => x.Id, x => x.Zkratka);

        var rows = permissions.Select(permission =>
        {
            var grants = previewContext.PermissionGrants
                .Where(x => Ci.Equals(x.PermissionKey, permission.Klic))
                .ToList();
            var scopeSummary = grants.Count == 0
                ? "-"
                : string.Join(", ", grants.Select(grant => BuildPermissionScopeSummary(grant.ScopeMode, grant.ProjectIds, projectCodesById)));
            var sourceSummary = grants.Count == 0
                ? "-"
                : string.Join(", ", grants
                    .Select(grant => BuildGrantSourceSummary(grant, projectCodesById))
                    .Where(summary => !string.IsNullOrWhiteSpace(summary))
                    .Distinct(StringComparer.CurrentCultureIgnoreCase));

            return new EffectivePermissionRowViewModel
            {
                PermissionKlic = permission.Klic,
                PermissionNazev = permission.Nazev,
                IsAllowed = previewContext.HasPermission(permission.Klic, selectedProjectId),
                ScopeSummary = scopeSummary,
                SourceSummary = string.IsNullOrWhiteSpace(sourceSummary) ? "-" : sourceSummary
            };
        }).ToList();

        return new EffectivePermissionsPreviewViewModel
        {
            SelectedUserId = selectedUser.Id,
            SelectedProjectId = selectedProjectId,
            OsobaId = selectedUser.Id,
            Osoba = BuildInlinePersonLabel(selectedUser.Titul, selectedUser.Jmeno, selectedUser.Prijmeni, selectedUser.Email, selectedUser.Id),
            ProjektId = selectedProjectId,
            ProjektNazev = selectedProjectId.HasValue
                ? projects.FirstOrDefault(x => x.Id == selectedProjectId.Value)?.CelyNazev ?? "-"
                : "Všechny projekty",
            AvailableUsers = users.Select(user => new EffectiveRightsFilterOptionViewModel
            {
                Id = user.Id,
                Label = BuildInlinePersonLabel(user.Titul, user.Jmeno, user.Prijmeni, user.Email, user.Id)
            }).ToList(),
            AvailableProjects = projects.Select(project => new EffectiveRightsFilterOptionViewModel
            {
                Id = project.Id,
                Label = project.CelyNazev
            }).ToList(),
            Rows = rows
        };
    }

    private string BuildDisplayName(string? titul, string jmeno, string prijmeni, int id)
    {
        var displayName = personIdentityMatcher.BuildPersonName(titul, jmeno, prijmeni);
        return string.IsNullOrWhiteSpace(displayName) ? $"Uživatel #{id}" : displayName;
    }

    private string BuildInlinePersonLabel(string? titul, string jmeno, string prijmeni, string? email, int id)
    {
        var displayName = BuildDisplayName(titul, jmeno, prijmeni, id);
        var normalizedEmail = textNormalizer.NormalizeEmail(email);
        return string.IsNullOrWhiteSpace(normalizedEmail)
            ? displayName
            : $"{displayName} <{normalizedEmail}>";
    }

    private static string BuildPermissionScopeSummary(
        string scopeMode,
        IReadOnlyList<int> projectIds,
        IReadOnlyDictionary<int, string> projectCodesById)
    {
        if (string.Equals(scopeMode, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            return "ALL";
        }

        if (!string.Equals(scopeMode, "INCLUDE", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(scopeMode) ? "-" : scopeMode.Trim().ToUpperInvariant();
        }

        if (projectIds.Count == 0)
        {
            return "INCLUDE (prázdné)";
        }

        var projectCodes = projectIds
            .Select(projectId => projectCodesById.GetValueOrDefault(projectId))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return projectCodes.Count == 0
            ? "INCLUDE (prázdné)"
            : "INCLUDE: " + string.Join("; ", projectCodes);
    }

    private static string BuildGrantSourceSummary(PermissionGrantViewModel grant, IReadOnlyDictionary<int, string> projectCodesById)
    {
        if (string.Equals(grant.SourceType, "APP_ROLE", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(grant.SourceRoleCode)
                ? "Aplikační role"
                : $"Aplikační role: {grant.SourceRoleCode}";
        }

        if (string.Equals(grant.SourceType, "PROJECT_ROLE", StringComparison.OrdinalIgnoreCase))
        {
            var roleCode = string.IsNullOrWhiteSpace(grant.SourceRoleCode) ? "projektová role" : grant.SourceRoleCode;
            if (grant.SourceProjectId.HasValue && projectCodesById.TryGetValue(grant.SourceProjectId.Value, out var projectCode))
            {
                return $"Projektová role: {roleCode} ({projectCode})";
            }

            return $"Projektová role: {roleCode}";
        }

        if (string.Equals(grant.SourceType, "SUBSYSTEM_ROLE", StringComparison.OrdinalIgnoreCase))
        {
            var roleCode = string.IsNullOrWhiteSpace(grant.SourceRoleCode) ? "subsystémová role" : grant.SourceRoleCode;
            if (grant.SourceProjectId.HasValue && projectCodesById.TryGetValue(grant.SourceProjectId.Value, out var projectCode))
            {
                return $"Subsystémová role: {roleCode} ({projectCode})";
            }

            return $"Subsystémová role: {roleCode}";
        }

        if (grant.ProjectIds.Count == 1 && projectCodesById.TryGetValue(grant.ProjectIds[0], out var singleProjectCode))
        {
            return $"Implicitní grant ({singleProjectCode})";
        }

        return "Implicitní grant";
    }
}
