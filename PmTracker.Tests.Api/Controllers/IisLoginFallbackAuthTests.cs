using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Data;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class IisLoginFallbackAuthTests
{
    private readonly ApiSqlFixture _fixture;

    public IisLoginFallbackAuthTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BrowserRequest_ShouldResolveUserByIisLogin_WhenPrincipalNameExistsButIdentityIsNotAuthenticated()
    {
        const string login = @"acr\pmtracker.iis.login";
        var originalLogin = await SetAdminLoginAsync(login);

        try
        {
            using var factory = new ProductionLoginFallbackWebAppFactory(_fixture.Database.ConnectionString);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var request = new HttpRequestMessage(HttpMethod.Get, "/Projekty");
            request.Headers.Add(HeaderDrivenTestAuthHandler.LoginHeaderName, login);
            request.Headers.Add(HeaderDrivenTestAuthHandler.AuthenticatedHeaderName, "false");

            var response = await client.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("Projekty");
        }
        finally
        {
            await SetAdminLoginAsync(originalLogin);
        }
    }

    [Fact]
    public async Task BrowserRequest_ShouldResolveUserByShortIisLogin_WhenStoredLoginContainsDomain()
    {
        const string storedLogin = @"acr\pmtracker.short.login";
        const string iisLogin = "pmtracker.short.login";
        var originalLogin = await SetAdminLoginAsync(storedLogin);

        try
        {
            using var factory = new ProductionLoginFallbackWebAppFactory(_fixture.Database.ConnectionString);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var request = new HttpRequestMessage(HttpMethod.Get, "/Projekty");
            request.Headers.Add(HeaderDrivenTestAuthHandler.LoginHeaderName, iisLogin);

            var response = await client.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("Projekty");
        }
        finally
        {
            await SetAdminLoginAsync(originalLogin);
        }
    }

    private async Task<string?> SetAdminLoginAsync(string? login)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var admin = await dbContext.Osoby.FirstAsync(x => x.Id == _fixture.AdminOsobaId);
        var originalLogin = admin.AdLogin;
        admin.AdLogin = login;
        await dbContext.SaveChangesAsync();
        return originalLogin;
    }

    private sealed class ProductionLoginFallbackWebAppFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");

            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PmTrackerDb"] = connectionString,
                    ["PmTracker:Data:Provider"] = "SqlServer",
                    ["PmTracker:Data:SqlServer:ConnectionStringName"] = "PmTrackerDb",
                    ["PmTracker:Data:SqlServer:CommandTimeoutSeconds"] = "60",
                    ["PmTracker:ActiveDirectory:Domain"] = "acr",
                    ["PmTracker:ActiveDirectory:MaxResults"] = "15",
                    ["PmTracker:ActiveDirectory:QueryTimeoutSeconds"] = "8"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAntiforgery>();
                services.AddSingleton<IAntiforgery, NoOpAntiforgery>();
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = HeaderDrivenTestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = HeaderDrivenTestAuthHandler.SchemeName;
                        options.DefaultForbidScheme = HeaderDrivenTestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, HeaderDrivenTestAuthHandler>(HeaderDrivenTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<MvcOptions>(options =>
                {
                    options.Filters.Add(new IgnoreAntiforgeryTokenAttribute());
                });
            });
        }
    }

    private sealed class HeaderDrivenTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "PmTrackerHeaderDrivenAuth";
        public const string LoginHeaderName = "X-PmTracker-Test-Login";
        public const string AuthenticatedHeaderName = "X-PmTracker-Test-Authenticated";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var login = Request.Headers[LoginHeaderName].ToString();
            if (string.IsNullOrWhiteSpace(login))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var authenticated = !string.Equals(
                Request.Headers[AuthenticatedHeaderName].ToString(),
                "false",
                StringComparison.OrdinalIgnoreCase);

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, login),
                    new Claim(ClaimTypes.WindowsAccountName, login)
                ],
                authenticated ? SchemeName : null);

            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
