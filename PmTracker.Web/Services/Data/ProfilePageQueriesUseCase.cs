using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Settings;

namespace PmTracker.Web.Services.Data;

public sealed class ProfilePageQueriesUseCase(
    PmTrackerDbContext dbContext,
    IUserAuthorizationSnapshotBuilder userAuthorizationSnapshotBuilder) : IProfilePageQueriesUseCase
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public ProfilPageViewModel BuildProfilPage(CurrentUserContextViewModel currentUser, int? projektId)
    {
        return new ProfilPageViewModel
        {
            Uzivatel = currentUser,
            MojeRole = BuildProfilRolePrava(currentUser.OsobaId),
            OdvozenaPrava = BuildProfilOdvozenaPrava(currentUser.OsobaId)
        };
    }

    private IReadOnlyList<ProfilOdvozenePravoViewModel> BuildProfilOdvozenaPrava(int osobaId)
    {
        var grants = userAuthorizationSnapshotBuilder.Build(osobaId).PermissionGrants
            .Where(grant => !Ci.Equals(grant.SourceType, "APP_ROLE"))
            .ToList();
        if (grants.Count == 0)
        {
            return [];
        }

        var projectCodesById = dbContext.Projekty.AsNoTracking()
            .ToDictionary(x => x.Id, x => x.Zkratka);
        var permissionNamesByKey = dbContext.AuthzPermissions.AsNoTracking()
            .Where(x => x.IsActive)
            .ToDictionary(x => x.Klic, x => x.Nazev, Ci);

        return grants
            .Select(grant => new ProfilOdvozenePravoViewModel
            {
                PermissionKlic = grant.PermissionKey,
                PermissionNazev = permissionNamesByKey.GetValueOrDefault(grant.PermissionKey, grant.PermissionKey),
                IsAllowed = grant.IsAllowed,
                ScopeSummary = BuildPermissionScopeSummary(grant.ScopeMode, grant.ProjectIds, projectCodesById),
                SourceSummary = BuildGrantSourceSummary(grant, projectCodesById)
            })
            .OrderBy(item => item.SourceSummary, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.PermissionKlic, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<ProfilRolePravaViewModel> BuildProfilRolePrava(int osobaId)
    {
        var roles = (
            from userRole in dbContext.AuthzUserRoles.AsNoTracking()
            join role in dbContext.AuthzRoles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.OsobaId == osobaId
                && userRole.IsActive
                && role.IsActive
            orderby role.Kod, role.Nazev
            select new
            {
                role.Id,
                role.Kod,
                role.Nazev,
                role.Popis
            })
            .Distinct()
            .ToList();

        if (roles.Count == 0)
        {
            return [];
        }

        var roleIds = roles.Select(x => x.Id).ToList();
        var rolePermissions = (
            from rolePermission in dbContext.AuthzRolePermissions.AsNoTracking()
            join permission in dbContext.AuthzPermissions.AsNoTracking() on rolePermission.PermissionId equals permission.Id
            where roleIds.Contains(rolePermission.RoleId)
                && permission.IsActive
            select new
            {
                rolePermission.Id,
                rolePermission.RoleId,
                permission.Klic,
                permission.Nazev,
                rolePermission.ScopeMode,
                rolePermission.IsAllowed
            })
            .ToList();

        var includeRolePermissionIds = rolePermissions
            .Where(x => Ci.Equals(x.ScopeMode, "INCLUDE"))
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var includedProjectIdsByRolePermissionId = dbContext.AuthzRolePermissionProjects.AsNoTracking()
            .Where(x => includeRolePermissionIds.Contains(x.RolePermissionId))
            .GroupBy(x => x.RolePermissionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(x => x.ProjektId).Distinct().ToList());

        var projectCodesById = dbContext.Projekty.AsNoTracking()
            .OrderBy(x => x.Zkratka)
            .Select(x => new
            {
                x.Id,
                x.Zkratka
            })
            .ToDictionary(x => x.Id, x => x.Zkratka);

        return roles
            .Select(role => new ProfilRolePravaViewModel
            {
                RoleId = role.Id,
                RoleKod = role.Kod,
                RoleNazev = role.Nazev,
                Popis = string.IsNullOrWhiteSpace(role.Popis) ? null : role.Popis.Trim(),
                Akce = rolePermissions
                    .Where(x => x.RoleId == role.Id)
                    .OrderBy(x => x.Klic, StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => new ProfilRoleAkceViewModel
                    {
                        PermissionKlic = x.Klic,
                        PermissionNazev = x.Nazev,
                        IsAllowed = x.IsAllowed,
                        ScopeSummary = BuildPermissionScopeSummary(
                            x.ScopeMode,
                            includedProjectIdsByRolePermissionId.GetValueOrDefault(x.Id, Array.Empty<int>()),
                            projectCodesById)
                    })
                    .ToList()
            })
            .ToList();
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
