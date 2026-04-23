using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PmTracker.Tests.Api.TestInfrastructure;

public sealed class PmTrackerWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public PmTrackerWebAppFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PmTrackerDb"] = _connectionString,
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

            // Integration testy sdílejí factory (CollectionFixture). Skutečný IMemoryCache
            // by držel snapshoty číselníků i po DB mutacích v setup hookách, což vede k falešně
            // prázdným výstupům (viz regression po zavedení LookupTableCache → IMemoryCache).
            // Testy pojedou bez cache — každé volání je čerstvé z DB.
            services.RemoveAll<IMemoryCache>();
            services.AddSingleton<IMemoryCache, NullMemoryCache>();
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<MvcOptions>(options =>
            {
                options.Filters.Add(new IgnoreAntiforgeryTokenAttribute());
            });
        });
    }
}
