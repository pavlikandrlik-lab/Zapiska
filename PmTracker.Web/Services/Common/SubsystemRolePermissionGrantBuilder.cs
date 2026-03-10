using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

public sealed record SubsystemRoleAssignmentGrantSource
{
    public required string RoleCode { get; init; }
    public int ProjectId { get; init; }
}

public static class SubsystemRolePermissionGrantBuilder
{
    public static IReadOnlyList<PermissionGrantViewModel> BuildImplicitSubsystemRoleGrants(IEnumerable<SubsystemRoleAssignmentGrantSource> activeAssignments)
    {
        ArgumentNullException.ThrowIfNull(activeAssignments);

        return activeAssignments
            .Where(assignment => assignment.ProjectId > 0)
            .Where(assignment =>
                string.Equals(assignment.RoleCode, SubsystemRoleCodes.Lead, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(assignment.RoleCode, SubsystemRoleCodes.DeputyLead, StringComparison.OrdinalIgnoreCase))
            .GroupBy(assignment => assignment.ProjectId)
            .Select(group =>
            {
                var sourceRoleCode = group.Any(assignment => string.Equals(assignment.RoleCode, SubsystemRoleCodes.Lead, StringComparison.OrdinalIgnoreCase))
                    ? SubsystemRoleCodes.Lead
                    : SubsystemRoleCodes.DeputyLead;

                return new PermissionGrantViewModel
                {
                    PermissionKey = PermissionKeys.RecordsCommentSubsystemLead,
                    ScopeLevel = "PROJECT",
                    ScopeMode = "INCLUDE",
                    IsAllowed = true,
                    ProjectIds = [group.Key],
                    SourceType = "SUBSYSTEM_ROLE",
                    SourceRoleCode = sourceRoleCode,
                    SourceProjectId = group.Key
                };
            })
            .ToList();
    }
}
