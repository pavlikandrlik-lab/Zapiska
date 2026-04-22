using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Middleware;

/// <summary>
/// Naplňuje <c>HttpContext.Items[CurrentUserAccessor.HttpContextItemKey]</c> s osoba_id
/// přihlášeného uživatele, a to PŘED fází <c>UseAuthorization()</c>. Tímto umožňuje
/// <c>PermissionAuthorizationHandler</c> číst osoba_id z <c>ICurrentUserAccessor</c>
/// při vyhodnocování <c>[Authorize(Policy = "permission:xxx")]</c> policies.
/// </summary>
/// <remarks>
/// <para>
/// Root cause fix (HIGH-1): do refaktoru byl resolve volán výhradně v
/// <c>BaseController.OnActionExecutionAsync</c> (action filter), který běží AŽ PO
/// <c>UseAuthorization()</c>. Výsledek: <c>HttpContext.Items[osobaId]</c> byl prázdný
/// v době vyhodnocení policy → <c>HandleRequirementAsync</c> nevolal <c>Succeed</c> →
/// framework vrátil 401/403 na každou [Authorize(Policy)] action.
/// </para>
/// <para>
/// Middleware delegations:
/// <list type="bullet">
/// <item>Úspěch resolve → zapíše osoba_id + celý <see cref="UserContextResolutionResult"/>
/// do HttpContext.Items. <see cref="Controllers.BaseController"/> čte cache z Items, aby
/// se resolver nevolal dvakrát (vyhne se duplicitní DB práci pro NavPermissions /
/// AccessDenied rendering).</item>
/// <item>Selhání resolve (anonymous, žádný mapping osoby, atd.) → klíče v Items nejsou
/// nastavené. Další middleware (<c>UseAuthorization</c>) standardně vrátí 401/403 pro
/// [Authorize] actions nebo propustí [AllowAnonymous] endpointy.</item>
/// <item>Neautentizovaný request bez <c>?asUser=</c> v produkci → middleware přeskočí
/// resolve úplně, aby se ušetřila DB práce u anonymních cest (statické soubory už
/// prošly přes <c>UseStaticFiles</c>, ale např. <c>/Home/Error</c> sem doputuje).</item>
/// </list>
/// </para>
/// </remarks>
public sealed class UserContextMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Klíč v <see cref="HttpContext.Items"/>, pod kterým je uložený celý
    /// <see cref="UserContextResolutionResult"/> pro reuse v <c>BaseController</c>.
    /// </summary>
    public const string UserContextCacheKey = "pmtracker.authz.resolvedContext";

    public async Task InvokeAsync(
        HttpContext context,
        IUserContextResolver resolver,
        IWebHostEnvironment environment)
    {
        if (ShouldResolve(context, environment))
        {
            var result = await resolver.ResolveAsync(context, context.RequestAborted);

            // Cache result (úspěch i selhání) pro BaseController, aby se resolver nevolal podruhé.
            context.Items[UserContextCacheKey] = result;

            if (result.IsSuccess && result.UserContext is not null)
            {
                // Poznámka: UserContextResolver.ResolveAsync už sám nastavuje tento klíč
                // (Fáze D Task D4, line 373). Necháváme explicitní zápis i zde, aby invariant
                // byl čitelný na úrovni middleware kontraktu, a aby úspěch nezávisel na
                // interní implementaci resolveru.
                context.Items[CurrentUserAccessor.HttpContextItemKey] = result.UserContext.OsobaId;
            }
        }

        await next(context);
    }

    private static bool ShouldResolve(HttpContext context, IWebHostEnvironment environment)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return true;
        }

        // Dev-only: asUser=<id> query override (viz UserContextResolver dev fallback).
        // Nezávisí na IsAuthenticated, takže musíme middleware explicitně probudit.
        if (environment.IsDevelopment())
        {
            if (!string.IsNullOrWhiteSpace(context.Request.Query["asUser"].ToString()))
            {
                return true;
            }
        }

        return false;
    }
}
