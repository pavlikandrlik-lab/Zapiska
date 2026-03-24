using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Profile;

namespace PmTracker.Tests.Unit.Profile;

public sealed class ProfileServiceTests
{
    [Fact]
    public void AddPmTrackerDataStore_ShouldResolveProfileFacadeAlias()
    {
        var services = new ServiceCollection();
        services.AddPmTrackerDataStore(BuildConfiguration());
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var concrete = scope.ServiceProvider.GetRequiredService<ProfileService>();
        var facade = scope.ServiceProvider.GetRequiredService<IProfileService>();

        facade.Should().BeSameAs(concrete);
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PmTracker"] = "Server=(localdb)\\mssqllocaldb;Database=PmTracker.Tests;Trusted_Connection=True;TrustServerCertificate=True;",
                ["PmTrackerData:Provider"] = "SqlServer",
                ["PmTrackerData:SqlServer:ConnectionStringName"] = "PmTracker",
                ["PmTrackerData:SqlServer:CommandTimeoutSeconds"] = "30"
            })
            .Build();
    }
}
