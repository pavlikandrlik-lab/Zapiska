using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

public sealed record ProjectRoleAssignmentGrantSource
{
    public required string RoleCode { get; init; }
    public int ProjectId { get; init; }
}

public static class ProjectRolePermissionGrantBuilder
{
    private static readonly HashSet<string> ProjectAdminLikeRoleCodes =
    [
        ProjectRoleCodes.ProjectAdmin,
        ProjectRoleCodes.ProjectManager
    ];

    private static readonly IReadOnlyList<string> ProjectAdminPermissionKeys =
    [
        PermissionKeys.TeamManage,
        PermissionKeys.RecordsEdit,
        PermissionKeys.MeetingsCreate,
        PermissionKeys.MeetingsEdit
    ];

    public static IReadOnlyList<PermissionGrantViewModel> BuildImplicitProjectRoleGrants(IEnumerable<ProjectRoleAssignmentGrantSource> activeAssignments)
    {
        ArgumentNullException.ThrowIfNull(activeAssignments);

        var grants = new List<PermissionGrantViewModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assignment in activeAssignments)
        {
            if (assignment.ProjectId <= 0)
            {
                continue;
            }

            if (!ProjectAdminLikeRoleCodes.Contains(assignment.RoleCode))
            {
                continue;
            }

            foreach (var permissionKey in ProjectAdminPermissionKeys)
            {
                var dedupeKey = $"{permissionKey}|{assignment.ProjectId}";
                if (!seen.Add(dedupeKey))
                {
                    continue;
                }

                grants.Add(new PermissionGrantViewModel
                {
                    PermissionKey = permissionKey,
                    ScopeLevel = "PROJECT",
                    ScopeMode = "INCLUDE",
                    IsAllowed = true,
                    ProjectIds = [assignment.ProjectId],
                    SourceType = "PROJECT_ROLE",
                    SourceRoleCode = assignment.RoleCode,
                    SourceProjectId = assignment.ProjectId
                });
            }
        }

        return grants;
    }
}
