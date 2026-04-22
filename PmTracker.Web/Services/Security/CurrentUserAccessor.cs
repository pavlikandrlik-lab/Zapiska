using Microsoft.AspNetCore.Http;

namespace PmTracker.Web.Services.Security;

/// <summary>
/// Scoped implementace čtoucí osobu z <c>HttpContext.Items["pmtracker.authz.osobaId"]</c>.
/// Key se nastavuje z UserContextResolver (Fáze D4) při každém requestu. Pokud není
/// naplněný, vrací null → autorizace selže jako anonymní.
/// </summary>
public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public const string HttpContextItemKey = "pmtracker.authz.osobaId";

    public int? OsobaId
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null) return null;

            if (httpContext.Items.TryGetValue(HttpContextItemKey, out var value) && value is int osobaId)
            {
                return osobaId;
            }

            return null;
        }
    }
}
