using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Modules.Settings;

public sealed class UserAuthorizationSnapshotBuilder(
    PmTrackerDbContext dbContext,
    ITextNormalizer textNormalizer) : IUserAuthorizationSnapshotBuilder
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public UserAuthorizationSnapshot Build(int osobaId)
    {
        var roleKody = BuildUserRoleCodes(osobaId);

        return new UserAuthorizationSnapshot
        {
            RoleKody = roleKody,
            PermissionGrants = BuildUserPermissionGrants(osobaId),
            VisibleProjectIds = BuildVisibleProjectIds(osobaId),
            DeletedProjectIds = BuildDeletedProjectIds(),
            IsSuperAdmin = dbContext.AuthzSuperadmins.AsNoTracking().Any(x => x.OsobaId == osobaId)
                || roleKody.Any(roleCode => Ci.Equals(roleCode, "SUPERADMIN"))
        };
    }

    private List<int> BuildVisibleProjectIds(int osobaId)
    {
        var projectRoleProjectIds = dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.OsobaId == osobaId && !x.DatumOdebrani.HasValue)
            .Select(x => x.ProjektId)
            .ToList();
        var subsystemRoleProjectIds = (
            from role in dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            join projectSubsystem in dbContext.ProjektSubsystemy.AsNoTracking() on role.ProjektSubsystemId equals projectSubsystem.Id
            where role.OsobaId == osobaId
                && !role.DatumOdebrani.HasValue
                && !projectSubsystem.DatumOdebrani.HasValue
            select projectSubsystem.ProjektId)
            .ToList();

        return projectRoleProjectIds
            .Concat(subsystemRoleProjectIds)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    private List<int> BuildDeletedProjectIds()
    {
        var directMatches = (
            from project in dbContext.Projekty.AsNoTracking()
            join status in dbContext.CiselnikStavuProjektu.AsNoTracking() on project.StavId equals status.Id
            where status.Kod == "DELETED"
            select project.Id)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (directMatches.Count > 0)
        {
            return directMatches;
        }

        return (
            from project in dbContext.Projekty.AsNoTracking()
            join status in dbContext.CiselnikStavuProjektu.AsNoTracking() on project.StavId equals status.Id
            select new { project.Id, status.Nazev })
            .AsEnumerable()
            .Where(x => textNormalizer.Normalize(x.Nazev).Contains("smaz"))
            .Select(x => x.Id)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    private List<string> BuildUserRoleCodes(int osobaId)
    {
        return dbContext.AuthzUserRoles.AsNoTracking()
            .Where(x => x.OsobaId == osobaId && x.IsActive)
            .Join(dbContext.AuthzRoles.AsNoTracking(), userRole => userRole.RoleId, role => role.Id, (userRole, role) => role)
            .Where(role => role.IsActive)
            .Select(role => role.Kod)
            .Distinct()
            .OrderBy(roleCode => roleCode)
            .ToList();
    }

    private List<PermissionGrantViewModel> BuildUserPermissionGrants(int osobaId)
    {
        var rolePermissions = (
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
            .ToList();

        var includeRolePermissionIds = rolePermissions
            .Where(x => Ci.Equals(x.ScopeMode, "INCLUDE"))
            .Select(x => x.RolePermissionId)
            .Distinct()
            .ToList();

        var includedProjectIdsByRolePermissionId = dbContext.AuthzRolePermissionProjects.AsNoTracking()
            .Where(x => includeRolePermissionIds.Contains(x.RolePermissionId))
            .GroupBy(x => x.RolePermissionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(item => item.ProjektId).Distinct().ToList());

        var explicitGrants = rolePermissions
            .Select(item => new PermissionGrantViewModel
            {
                PermissionKey = item.Klic,
                ScopeLevel = item.ScopeLevel,
                ScopeMode = item.ScopeMode,
                IsAllowed = item.IsAllowed,
                ProjectIds = Ci.Equals(item.ScopeMode, "INCLUDE")
                    ? includedProjectIdsByRolePermissionId.GetValueOrDefault(item.RolePermissionId, [])
                    : [],
                SourceType = "APP_ROLE",
                SourceRoleCode = item.RoleKod
            })
            .ToList();

        var implicitProjectRoleGrants = ProjectRolePermissionGrantBuilder.BuildImplicitProjectRoleGrants(
            (
                from assignment in dbContext.ObsazeniProjektu.AsNoTracking()
                join role in dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals role.Id
                where assignment.OsobaId == osobaId
                    && !assignment.DatumOdebrani.HasValue
                select new ProjectRoleAssignmentGrantSource
                {
                    RoleCode = role.Kod,
                    ProjectId = assignment.ProjektId
                })
            .ToList());

        var implicitSubsystemRoleGrants = SubsystemRolePermissionGrantBuilder.BuildImplicitSubsystemRoleGrants(
            (
                from assignment in dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
                join role in dbContext.CiselnikRoliSubsystemu.AsNoTracking() on assignment.RoleSubsystemuId equals role.Id
                join projectSubsystem in dbContext.ProjektSubsystemy.AsNoTracking() on assignment.ProjektSubsystemId equals projectSubsystem.Id
                where assignment.OsobaId == osobaId
                    && !assignment.DatumOdebrani.HasValue
                    && !projectSubsystem.DatumOdebrani.HasValue
                select new SubsystemRoleAssignmentGrantSource
                {
                    RoleCode = role.Kod,
                    ProjectId = projectSubsystem.ProjektId
                })
            .ToList());

        return explicitGrants
            .Concat(implicitProjectRoleGrants)
            .Concat(implicitSubsystemRoleGrants)
            .ToList();
    }
}
