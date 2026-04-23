using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PmTracker.Tests.Api.TestInfrastructure;

/// <summary>
/// Integration-test authentication scheme. Vrací vždy Success s bare
/// <see cref="ClaimsPrincipal"/> (authentication-typed identity) tak, aby pipeline
/// dospělo k <c>UseAuthorization()</c>. Skutečná identita osoby (osoba_id) se resolvuje
/// dev-only přes <c>?asUser=</c> query parametr v <see cref="UserContextMiddleware"/>.
/// </summary>
/// <remarks>
/// <para>
/// Bez authentication success by Authorization vracela <c>Challenge(401)</c> místo
/// <c>Forbid(403)</c> — testy <c>AccessDeniedPageTests</c>, ověřující 403 u neznámého
/// uživatele, by selhaly. Real production flow používá <c>IISDefaults.AuthenticationScheme</c>,
/// který authenticatuje uživatele přes Windows/Kerberos; Test flow tu autentizaci simuluje
/// minimálně a deleguje identity lookup na <c>?asUser=</c> fallback.
/// </para>
/// </remarks>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PmTrackerTestAuth";

    /// <summary>HTTP header, který test může poslat pro simulaci autentikovaného uživatele bez <c>?asUser=</c>.</summary>
    public const string ImpersonateHeaderName = "X-Test-Impersonate";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var asUser = Request.Query["asUser"].ToString();
        var impersonate = Request.Headers[ImpersonateHeaderName].ToString();
        var principalName = !string.IsNullOrWhiteSpace(asUser) ? asUser
            : !string.IsNullOrWhiteSpace(impersonate) ? impersonate
            : null;

        if (string.IsNullOrWhiteSpace(principalName))
        {
            // Anonymní request — test explicitně neoznámil identitu.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.Name, principalName));
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
