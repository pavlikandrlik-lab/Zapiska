using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Profile;

public sealed partial class ProfileService
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public async Task<ProfilPageViewModel> BuildProfilPageAsync(CurrentUserContextViewModel currentUser, int? projektId, CancellationToken ct = default)
    {
        return new ProfilPageViewModel
        {
            Uzivatel = currentUser,
            MojeRole = await BuildProfilRoleInstancesAsync(currentUser.OsobaId, ct)
        };
    }

    /// <summary>
    /// A5 (2026-07-08): jednotné karty per INSTANCE role. Aplikační role z AuthzUserRoles;
    /// projektové/subsystémové PŘÍMÝM dotazem přes lookup role → AuthzRoleId → AuthzRolePermissions
    /// (vzor UserContextResolver). Záměrně NE přes PermissionGrants audit snapshotu — ty jsou
    /// grupované per permission klíč a identita role instance se v nich ztrácí.
    /// </summary>
    private async Task<IReadOnlyList<ProfilRoleInstanceViewModel>> BuildProfilRoleInstancesAsync(int osobaId, CancellationToken ct)
    {
        var app = await BuildAppRoleInstancesAsync(osobaId, ct);
        var project = await BuildProjectRoleInstancesAsync(osobaId, ct);
        var subsystem = await BuildSubsystemRoleInstancesAsync(osobaId, ct);
        // Pořadí: Aplikační → Projektová → Subsystémová (uvnitř řazeno v builderech).
        return app.Concat(project).Concat(subsystem).ToList();
    }

    private async Task<IReadOnlyList<ProfilRoleInstanceViewModel>> BuildAppRoleInstancesAsync(int osobaId, CancellationToken ct)
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

        var projectCodesById = await dbContext.Projekty.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Zkratka, ct);

        return roles
            .Select(role => new ProfilRoleInstanceViewModel
            {
                TypRole = "Aplikační",
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

    private async Task<IReadOnlyList<ProfilRoleInstanceViewModel>> BuildProjectRoleInstancesAsync(int osobaId, CancellationToken ct)
    {
        var rows = await (
            from assignment in dbContext.ObsazeniProjektu.AsNoTracking()
            where assignment.OsobaId == osobaId && !assignment.DatumOdebrani.HasValue
            join lookupRole in dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals lookupRole.Id
            where lookupRole.AuthzRoleId != null
            join projekt in dbContext.Projekty.AsNoTracking() on assignment.ProjektId equals projekt.Id
            join authzRole in dbContext.AuthzRoles.AsNoTracking() on lookupRole.AuthzRoleId equals authzRole.Id
            where authzRole.IsActive
            join rp in dbContext.AuthzRolePermissions.AsNoTracking() on authzRole.Id equals rp.RoleId
            join permission in dbContext.AuthzPermissions.AsNoTracking() on rp.PermissionId equals permission.Id
            where permission.IsActive
            select new
            {
                RoleKod = lookupRole.Kod,
                RoleNazev = lookupRole.Nazev,
                ProjektZkratka = projekt.Zkratka,
                ProjektNazev = projekt.CelyNazev,
                permission.Klic,
                PermissionNazev = permission.Nazev,
                rp.IsAllowed
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => new { r.RoleKod, r.RoleNazev, r.ProjektZkratka, r.ProjektNazev })
            .OrderBy(g => g.Key.ProjektZkratka, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.Key.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ProfilRoleInstanceViewModel
            {
                TypRole = "Projektová",
                RoleKod = g.Key.RoleKod,
                RoleNazev = g.Key.RoleNazev,
                ProjektZkratka = g.Key.ProjektZkratka,
                ProjektNazev = g.Key.ProjektNazev,
                Akce = g
                    .GroupBy(x => x.Klic)
                    .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => new ProfilRoleAkceViewModel
                    {
                        PermissionKlic = x.Key,
                        PermissionNazev = x.First().PermissionNazev,
                        IsAllowed = x.Any(y => y.IsAllowed),
                        ScopeSummary = g.Key.ProjektZkratka
                    })
                    .ToList()
            })
            .ToList();
    }

    private async Task<IReadOnlyList<ProfilRoleInstanceViewModel>> BuildSubsystemRoleInstancesAsync(int osobaId, CancellationToken ct)
    {
        var rows = await (
            from assignment in dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            where assignment.OsobaId == osobaId && !assignment.DatumOdebrani.HasValue
            join lookupRole in dbContext.CiselnikRoliSubsystemu.AsNoTracking() on assignment.RoleSubsystemuId equals lookupRole.Id
            where lookupRole.AuthzRoleId != null
            join projectSubsystem in dbContext.ProjektSubsystemy.AsNoTracking() on assignment.ProjektSubsystemId equals projectSubsystem.Id
            where !projectSubsystem.DatumOdebrani.HasValue
            join projekt in dbContext.Projekty.AsNoTracking() on projectSubsystem.ProjektId equals projekt.Id
            join subsystem in dbContext.Subsystemy.AsNoTracking() on projectSubsystem.SubsystemId equals subsystem.Id
            join authzRole in dbContext.AuthzRoles.AsNoTracking() on lookupRole.AuthzRoleId equals authzRole.Id
            where authzRole.IsActive
            join rp in dbContext.AuthzRolePermissions.AsNoTracking() on authzRole.Id equals rp.RoleId
            join permission in dbContext.AuthzPermissions.AsNoTracking() on rp.PermissionId equals permission.Id
            where permission.IsActive
            select new
            {
                RoleKod = lookupRole.Kod,
                RoleNazev = lookupRole.Nazev,
                ProjektZkratka = projekt.Zkratka,
                ProjektNazev = projekt.CelyNazev,
                SubsystemKod = subsystem.Kod,
                SubsystemNazev = subsystem.Nazev,
                permission.Klic,
                PermissionNazev = permission.Nazev,
                rp.IsAllowed
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => new { r.RoleKod, r.RoleNazev, r.ProjektZkratka, r.ProjektNazev, r.SubsystemKod, r.SubsystemNazev })
            .OrderBy(g => g.Key.ProjektZkratka, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.Key.SubsystemKod, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.Key.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ProfilRoleInstanceViewModel
            {
                TypRole = "Subsystémová",
                RoleKod = g.Key.RoleKod,
                RoleNazev = g.Key.RoleNazev,
                ProjektZkratka = g.Key.ProjektZkratka,
                ProjektNazev = g.Key.ProjektNazev,
                SubsystemKod = g.Key.SubsystemKod,
                SubsystemNazev = g.Key.SubsystemNazev,
                Akce = g
                    .GroupBy(x => x.Klic)
                    .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => new ProfilRoleAkceViewModel
                    {
                        PermissionKlic = x.Key,
                        PermissionNazev = x.First().PermissionNazev,
                        IsAllowed = x.Any(y => y.IsAllowed),
                        ScopeSummary = $"{g.Key.ProjektZkratka} / {g.Key.SubsystemKod}"
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
}
