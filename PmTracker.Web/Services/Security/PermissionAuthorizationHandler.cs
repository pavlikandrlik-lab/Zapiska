using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace PmTracker.Web.Services.Security;

/// <summary>
/// ASP.NET Core authorization handler pro <see cref="PermissionRequirement"/>. Deleguje kontrolu
/// na <see cref="IAuthorizationService"/> a extrahuje projektId/projektSubsystemId z route kontextu.
/// </summary>
/// <remarks>
/// Policies registrované v DI jako "permission:xxx.yyy" (viz <c>AuthorizationPolicyExtensions</c>).
/// Kontext čtený z route: <c>projektId</c> a <c>projektSubsystemId</c>. Pokud klíč chybí, snapshot
/// pracuje jako global-scope checkem. Pokud klíč PŘÍTOMEN ale neparsovatelný, autorizace selže
/// closed (return bez Succeed) — M2 fix.
/// </remarks>
public sealed class PermissionAuthorizationHandler(
    IAuthorizationService authzService,
    ICurrentUserAccessor currentUser,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var osobaId = currentUser.OsobaId;
        if (osobaId is null)
        {
            return; // anonymous / unresolved — fail without calling Succeed
        }

        int? projektId = null;
        int? subsystemId = null;

        var route = httpContextAccessor.HttpContext?.Request.RouteValues;
        if (route is not null)
        {
            if (route.TryGetValue("projektId", out var p) && p is not null)
            {
                if (!int.TryParse(p.ToString(), out var pid))
                {
                    return; // malformed projektId -> fail closed (M2)
                }
                projektId = pid;
            }
            if (route.TryGetValue("projektSubsystemId", out var s) && s is not null)  // M4: subsystemId → projektSubsystemId
            {
                if (!int.TryParse(s.ToString(), out var sid))
                {
                    return; // malformed projektSubsystemId -> fail closed (M2)
                }
                subsystemId = sid;
            }
        }

        var ct = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        if (await authzService.HasPermissionAsync(osobaId.Value, requirement.PermissionKey, projektId, subsystemId, ct))
        {
            context.Succeed(requirement);
        }
    }
}
