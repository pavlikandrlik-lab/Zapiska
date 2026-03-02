using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace PmTracker.Tests.Api.TestInfrastructure;

internal sealed class NoOpAntiforgery : IAntiforgery
{
    private static readonly AntiforgeryTokenSet Tokens = new("test-request-token", "test-cookie-token", "__RequestVerificationToken", "X-CSRF-TOKEN");

    public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => Tokens;

    public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => Tokens;

    public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);

    public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;

    public void SetCookieTokenAndHeader(HttpContext httpContext)
    {
    }
}
