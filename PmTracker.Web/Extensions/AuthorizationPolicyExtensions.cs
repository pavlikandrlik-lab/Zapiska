using Microsoft.AspNetCore.Authorization;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Extensions;

/// <summary>
/// Registrace autorizačních policies pro každý permission key z <see cref="PermissionKeys.AllDefinitions"/>.
/// Policy name convention: <c>permission:{key}</c>, např. <c>permission:records.edit</c>.
/// </summary>
public static class AuthorizationPolicyExtensions
{
    /// <summary>Prefix for all permission-based policies.</summary>
    public const string PolicyNamePrefix = "permission:";

    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        foreach (var definition in PermissionKeys.AllDefinitions)
        {
            options.AddPolicy(
                $"{PolicyNamePrefix}{definition.Key}",
                policy => policy.Requirements.Add(new PermissionRequirement(definition.Key)));
        }
    }
}
