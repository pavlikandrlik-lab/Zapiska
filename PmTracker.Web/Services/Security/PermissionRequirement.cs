using Microsoft.AspNetCore.Authorization;

namespace PmTracker.Web.Services.Security;

/// <summary>
/// Authorization requirement capturing the permission key that must be satisfied for a policy.
/// Used by <see cref="PermissionAuthorizationHandler"/>.
/// </summary>
public sealed class PermissionRequirement(string permissionKey) : IAuthorizationRequirement
{
    public string PermissionKey { get; } = permissionKey;
}
