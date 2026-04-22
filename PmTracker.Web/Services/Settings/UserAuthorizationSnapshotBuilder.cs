using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Services.Settings;

public sealed class UserAuthorizationSnapshotBuilder(
    PmTrackerDbContext dbContext,
    ITextNormalizer textNormalizer) : IUserAuthorizationSnapshotBuilder
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public async Task<UserAuthorizationSnapshot> BuildAsync(int osobaId, CancellationToken ct = default)
    {
        var roleKody = await BuildUserRoleCodesAsync(osobaId, ct);

        return new UserAuthorizationSnapshot
        {
            RoleKody = roleKody,
            PermissionGrants = await BuildUserPermissionGrantsAsync(osobaId, ct),
            VisibleProjectIds = await BuildVisibleProjectIdsAsync(osobaId, ct),
            DeletedProjectIds = await BuildDeletedProjectIdsAsync(ct),
            IsSuperAdmin = await dbContext.AuthzSuperadmins.AsNoTracking()
                .AnyAsync(x => x.OsobaId == osobaId, ct)
        };
    }

    private async Task<List<int>> BuildVisibleProjectIdsAsync(int osobaId, CancellationToken ct)
    {
        var projectRoleProjectIds = await dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.OsobaId == osobaId && !x.DatumOdebrani.HasValue)
            .Select(x => x.ProjektId)
            .ToListAsync(ct);
        var subsystemRoleProjectIds = await (
            from role in dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            join projectSubsystem in dbContext.ProjektSubsystemy.AsNoTracking() on role.ProjektSubsystemId equals projectSubsystem.Id
            where role.OsobaId == osobaId
                && !role.DatumOdebrani.HasValue
                && !projectSubsystem.DatumOdebrani.HasValue
            select projectSubsystem.ProjektId)
            .ToListAsync(ct);

        return projectRoleProjectIds
            .Concat(subsystemRoleProjectIds)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    private async Task<List<int>> BuildDeletedProjectIdsAsync(CancellationToken ct)
        => await ProjectAuthorizationQueryHelper.BuildDeletedProjectIdsAsync(dbContext, textNormalizer, ct);

    private Task<List<string>> BuildUserRoleCodesAsync(int osobaId, CancellationToken ct)
    {
        return dbContext.AuthzUserRoles.AsNoTracking()
            .Where(x => x.OsobaId == osobaId && x.IsActive)
            .Join(dbContext.AuthzRoles.AsNoTracking(), userRole => userRole.RoleId, role => role.Id, (userRole, role) => role)
            .Where(role => role.IsActive)
            .Select(role => role.Kod)
            .Distinct()
            .OrderBy(roleCode => roleCode)
            .ToListAsync(ct);
    }

    private async Task<List<PermissionGrantViewModel>> BuildUserPermissionGrantsAsync(int osobaId, CancellationToken ct)
    {
        var rolePermissions = await (
            from userRole in dbContext.AuthzUserRoles.AsNoTracking()
            join role in dbContext.AuthzRoles.AsNoTracking() on userRole.RoleId equals role.Id
            join rolePermission in dbContext.AuthzRolePermissions.AsNoTracking() on userRole.RoleId equals rolePermission.RoleId
            join permission in dbContext.AuthzPermissions.AsNoTracking() on rolePermission.PermissionId equals permission.Id
            where userRole.OsobaId == osobaId && userRole.IsActive && role.IsActive && permission.IsActive
            select new
            {
                RolePermissionId = rolePermission.Id,
                RoleKod = role.Kod,
                permission.Klic,
                permission.ScopeLevel,
                rolePermission.ScopeMode,
                rolePermission.IsAllowed
            })
            .ToListAsync(ct);

        var includeRolePermissionIds = rolePermissions
            .Where(x => Ci.Equals(x.ScopeMode.ToString().ToUpperInvariant(), "INCLUDE"))
            .Select(x => x.RolePermissionId)
            .Distinct()
            .ToList();

        var includedProjectRows = await dbContext.AuthzRolePermissionProjects.AsNoTracking()
            .Where(x => includeRolePermissionIds.Contains(x.RolePermissionId))
            .ToListAsync(ct);
        var includedProjectIdsByRolePermissionId = includedProjectRows
            .GroupBy(x => x.RolePermissionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(item => item.ProjektId).Distinct().ToList());

        var explicitGrants = rolePermissions
            .Select(item => new PermissionGrantViewModel
            {
                PermissionKey = item.Klic,
                ScopeLevel = item.ScopeLevel.ToString().ToUpperInvariant(),
                ScopeMode = item.ScopeMode.ToString().ToUpperInvariant(),
                IsAllowed = item.IsAllowed,
                ProjectIds = Ci.Equals(item.ScopeMode.ToString().ToUpperInvariant(), "INCLUDE")
                    ? includedProjectIdsByRolePermissionId.GetValueOrDefault(item.RolePermissionId, [])
                    : [],
                SourceType = "APP_ROLE",
                SourceRoleCode = item.RoleKod
            })
            .ToList();

        // Fáze B Task B3: DB-driven granty z projektových a subsystémových rolí (buildery smazány).
        var implicitProjectRoleGrants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(dbContext, osobaId, ct);
        var implicitSubsystemRoleGrants = await UserContextResolver.LoadDbDrivenSubsystemRoleGrantsAsync(dbContext, osobaId, ct);

        return explicitGrants
            .Concat(implicitProjectRoleGrants)
            .Concat(implicitSubsystemRoleGrants)
            .ToList();
    }
}
