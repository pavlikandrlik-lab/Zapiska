using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.ProjectDashboard;

public static class ProjectDashboardAuthorizationPolicy
{
    private static readonly HashSet<string> DashboardRoleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ProjectRoleCodes.ProjectManager,
        ProjectRoleCodes.ProjectAdmin,
        ProjectRoleCodes.Gestor
    };

    public static bool HasDashboardAccess(IReadOnlyList<string> activeProjectRoleCodes)
    {
        return activeProjectRoleCodes.Any(code => DashboardRoleCodes.Contains(code));
    }
}
