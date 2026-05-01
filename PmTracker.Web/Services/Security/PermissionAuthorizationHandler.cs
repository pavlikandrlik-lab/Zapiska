using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace PmTracker.Web.Services.Security;

/// <summary>
/// ASP.NET Core authorization handler pro <see cref="PermissionRequirement"/>. Deleguje kontrolu
/// na <see cref="IAuthorizationService"/> a extrahuje projektId/projektSubsystemId z HTTP requestu.
/// </summary>
/// <remarks>
/// Policies registrované v DI jako "permission:xxx.yyy" (viz <c>AuthorizationPolicyExtensions</c>).
///
/// Hledání kontextových klíčů (<c>projektId</c>, <c>projektSubsystemId</c>) — fix 2026-04-30:
/// hodnoty čteme v pořadí <b>Route → Query → Form</b>. Důvod: některé controllery mají projektId
/// jako route param (např. ProjectDashboard, Export), jiné jako query string (NavrhyController) nebo
/// form body (POST endpointy). Bez fallback čtení handler dostával null pro non-route zdroje a
/// project-scoped permission selhala na global check → 403 pro běžné users i s validní rolí.
///
/// Pokud klíč chybí ve všech zdrojích, snapshot pracuje jako global-scope check.
/// Pokud klíč PŘÍTOMEN v některém zdroji ale neparsovatelný, autorizace selže closed (M2 fix).
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

        var http = httpContextAccessor.HttpContext;

        var (projektOk, projektId) = TryReadIntFromRequest(http, "projektId");
        if (!projektOk)
        {
            return; // malformed projektId -> fail closed (M2)
        }

        var (subsystemOk, subsystemId) = TryReadIntFromRequest(http, "projektSubsystemId");
        if (!subsystemOk)
        {
            return; // malformed projektSubsystemId -> fail closed (M2)
        }

        var ct = http?.RequestAborted ?? CancellationToken.None;
        if (await authzService.HasPermissionAsync(osobaId.Value, requirement.PermissionKey, projektId, subsystemId, ct))
        {
            context.Succeed(requirement);
        }
    }

    /// <summary>
    /// Hledá int hodnotu pro daný klíč v Route → Query → Form. První zdroj, kde se klíč objeví,
    /// určuje výsledek. Vrátí (ok, value):
    /// <list type="bullet">
    ///   <item>(true, null) — klíč není přítomný v žádném zdroji (global-scope check).</item>
    ///   <item>(true, value) — klíč nalezen a úspěšně parsován jako int.</item>
    ///   <item>(false, null) — klíč přítomný ale neparsovatelný (caller selže closed).</item>
    /// </list>
    /// </summary>
    private static (bool Ok, int? Value) TryReadIntFromRequest(HttpContext? http, string key)
    {
        if (http is null)
        {
            return (true, null);
        }

        // 1. Route values (e.g. [Route("projekty/{projektId:int}/...")])
        if (http.Request.RouteValues.TryGetValue(key, out var routeValue) && routeValue is not null)
        {
            return TryParseScalar(routeValue.ToString());
        }

        // 2. Query string (e.g. /Navrhy/CreateScheduleProposal?projektId=...)
        if (http.Request.Query.TryGetValue(key, out var queryValue) && !StringValues.IsNullOrEmpty(queryValue))
        {
            return TryParseScalar(queryValue.ToString());
        }

        // 3. Form body (e.g. POST [FromForm] commands)
        if (http.Request.HasFormContentType
            && http.Request.Form.TryGetValue(key, out var formValue)
            && !StringValues.IsNullOrEmpty(formValue))
        {
            return TryParseScalar(formValue.ToString());
        }

        return (true, null);
    }

    private static (bool Ok, int? Value) TryParseScalar(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (true, null);
        }
        return int.TryParse(raw, out var parsed)
            ? (true, parsed)
            : (false, null);
    }
}
