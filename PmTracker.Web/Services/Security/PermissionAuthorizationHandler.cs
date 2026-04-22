using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace PmTracker.Web.Services.Security;

/// <summary>
/// ASP.NET Core authorization handler pro <see cref="PermissionRequirement"/>. Deleguje kontrolu
/// na <see cref="IAuthorizationService"/> a extrahuje projektId/subsystemId z route kontextu.
/// </summary>
/// <remarks>
/// Policies registrované v DI jako "permission:xxx.yyy" (viz <c>AuthorizationPolicyExtensions</c>).
/// Kontext čtený z route: <c>projektId</c> a <c>subsystemId</c>. Pokud chybí, snapshot pracuje
/// jako s global-scope checkem (nekontroluje projekt kontext).
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
            if (route.TryGetValue("projektId", out var p) && int.TryParse(p?.ToString(), out var pid))
            {
                projektId = pid;
            }
            if (route.TryGetValue("subsystemId", out var s) && int.TryParse(s?.ToString(), out var sid))
            {
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
