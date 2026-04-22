using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Web.Data;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Unit.Services.Data;

public sealed class DbContextRetryTests
{
    [Fact]
    public void DbContextOptions_FromDi_ShouldHaveRetryingStrategy()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PmTracker:Data:Provider"] = "SqlServer",
                ["PmTracker:Data:SqlServer:ConnectionStringName"] = "Pm",
                ["PmTracker:Data:SqlServer:CommandTimeoutSeconds"] = "30",
                ["ConnectionStrings:Pm"] = "Server=(localdb)\\mssqllocaldb;Database=__test__;Integrated Security=true"
            })
            .Build();
        services.AddPmTrackerDataStore(config);

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        var strategy = db.Database.CreateExecutionStrategy();
        strategy.RetriesOnFailure.Should().BeTrue(
            "production DI must configure EnableRetryOnFailure so transient DB failures are retried; " +
            "all user-initiated transactions must wrap in CreateExecutionStrategy().ExecuteAsync");
    }
}
