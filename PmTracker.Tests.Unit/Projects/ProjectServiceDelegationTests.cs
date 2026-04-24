using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.ServiceDesk.Sql;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Unit.Projects;

public sealed class ProjectServiceDelegationTests
{
    [Fact]
    public void AddPmTrackerDataStore_ShouldResolveProjectFacadeAndCompositionAliases()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddPmTrackerDataStore(configuration);
        // Plán 4 Feature C Task 6 (2026-04-24): ProjectService injectuje
        // IHarmonogramSkutecnostSyncService, který závisí na IVyjadreniQueryService.
        // IVyjadreniQueryService registruje až AddServiceDeskIntegration, takže obě
        // extensions musíme zavolat v testech v pořadí stejném jako v Program.cs.
        services.AddServiceDeskIntegration(configuration);
        // IHarmonogramSkutecnostSyncService je registrován v Program.cs přímo — test si to musí
        // replikovat explicitně (není v žádném extension aby nevznikla circular ref mezi vrstvami).
        services.AddScoped<
            PmTracker.Web.Services.Schedules.IHarmonogramSkutecnostSyncService,
            PmTracker.Web.Services.Schedules.HarmonogramSkutecnostSyncService>();
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
