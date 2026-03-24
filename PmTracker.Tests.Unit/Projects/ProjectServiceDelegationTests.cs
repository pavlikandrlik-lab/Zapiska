using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Unit.Projects;

public sealed class ProjectServiceDelegationTests
{
    [Fact]
    public void AddPmTrackerDataStore_ShouldResolveProjectFacadeAndCompositionAliases()
    {
        var services = new ServiceCollection();
        services.AddPmTrackerDataStore(BuildConfiguration());
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var concrete = scope.ServiceProvider.GetRequiredService<ProjectService>();

        scope.ServiceProvider.GetRequiredService<IProjectService>().Should().BeSameAs(concrete);
        scope.ServiceProvider.GetRequiredService<IProjectDetailComposition>().Should().BeSameAs(concrete);
        scope.ServiceProvider.GetRequiredService<IRecordEditorQueriesComposition>().Should().BeSameAs(concrete);
        scope.ServiceProvider.GetRequiredService<IRecordWriteCommandsComposition>().Should().BeSameAs(concrete);
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
