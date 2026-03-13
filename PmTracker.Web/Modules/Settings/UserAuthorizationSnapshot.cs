using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Settings;

public sealed class UserAuthorizationSnapshot
{
    public required IReadOnlyList<string> RoleKody { get; init; }
    public required IReadOnlyList<PermissionGrantViewModel> PermissionGrants { get; init; }
    public required IReadOnlyList<int> VisibleProjectIds { get; init; }
    public required IReadOnlyList<int> DeletedProjectIds { get; init; }
    public required bool IsSuperAdmin { get; init; }
}
