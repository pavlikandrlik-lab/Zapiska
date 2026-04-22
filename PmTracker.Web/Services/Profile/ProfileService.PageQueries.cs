using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Settings;

namespace PmTracker.Web.Services.Profile;

public sealed partial class ProfileService
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public async Task<ProfilPageViewModel> BuildProfilPageAsync(CurrentUserContextViewModel currentUser, int? projektId, CancellationToken ct = default)
    {
        return new ProfilPageViewModel
        {
            Uzivatel = currentUser,
            MojeRole = await BuildProfilRolePravaAsync(currentUser.OsobaId, ct),
            OdvozenaPrava = await BuildProfilOdvozenaPravaAsync(currentUser.OsobaId, ct)
        };
    }

    private async Task<IReadOnlyList<ProfilOdvozenePravoViewModel>> BuildProfilOdvozenaPravaAsync(int osobaId, CancellationToken ct)
    {
        var grants = (await userAuthorizationSnapshotBuilder.BuildAsync(osobaId, ct)).PermissionGrants
            .Where(grant => !Ci.Equals(grant.SourceType, "APP_ROLE"))
            .ToList();
        if (grants.Count == 0)
        {
            return [];
        }

        var projectCodesById = await dbContext.Projekty.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Zkratka, ct);
        var permissionNamesByKey = await dbContext.AuthzPermissions.AsNoTracking()
            .Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.Klic, x => x.Nazev, Ci, ct);

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

    private async Task<IReadOnlyList<ProfilRolePravaViewModel>> BuildProfilRolePravaAsync(int osobaId, CancellationToken ct)
    {
        var roles = await (
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
            .ToListAsync(ct);

        if (roles.Count == 0)
        {
            return [];
        }

        var roleIds = roles.Select(x => x.Id).ToList();
        var rolePermissions = await (
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
            .ToListAsync(ct);

        var includeRolePermissionIds = rolePermissions
            .Where(x => Ci.Equals(x.ScopeMode.ToString().ToUpperInvariant(), "INCLUDE"))
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var includedProjectRows = await dbContext.AuthzRolePermissionProjects.AsNoTracking()
            .Where(x => includeRolePermissionIds.Contains(x.RolePermissionId))
            .ToListAsync(ct);
        var includedProjectIdsByRolePermissionId = includedProjectRows
            .GroupBy(x => x.RolePermissionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(x => x.ProjektId).Distinct().ToList());

        var projectRows = await dbContext.Projekty.AsNoTracking()
            .OrderBy(x => x.Zkratka)
            .Select(x => new
            {
                x.Id,
                x.Zkratka
            })
            .ToListAsync(ct);
        var projectCodesById = projectRows.ToDictionary(x => x.Id, x => x.Zkratka);

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
                            x.ScopeMode.ToString().ToUpperInvariant(),
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
