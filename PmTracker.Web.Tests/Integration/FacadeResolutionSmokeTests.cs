using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.ServiceDesk.Sql;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Dictionaries;
using PmTracker.Web.Services.People;
using PmTracker.Web.Services.Profile;

namespace PmTracker.Web.Tests.Integration;

public sealed class FacadeResolutionSmokeTests
{
    [Fact]
    public void AddPmTrackerDataStore_ShouldResolveFacadeAliasesPerRequest()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddPmTrackerDataStore(configuration);
        // Plán 3 Feature D (2026-04-24): RecordService závisí na ExterniOdkazValidator,
        // který potřebuje IVyjadreniQueryService z AddServiceDeskIntegration. Pořadí
        // registrací matches Program.cs.
        services.AddServiceDeskIntegration(configuration);
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var meetingService = scope.ServiceProvider.GetRequiredService<MeetingService>();
        Assert.Same(meetingService, scope.ServiceProvider.GetRequiredService<IMeetingService>());

        var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
        Assert.Same(projectService, scope.ServiceProvider.GetRequiredService<IProjectService>());
        Assert.Same(projectService, scope.ServiceProvider.GetRequiredService<IProjectDetailComposition>());
        Assert.Same(projectService, scope.ServiceProvider.GetRequiredService<IRecordEditorQueriesComposition>());
        Assert.Same(projectService, scope.ServiceProvider.GetRequiredService<IRecordWriteCommandsComposition>());

        var recordService = scope.ServiceProvider.GetRequiredService<RecordService>();
        Assert.Same(recordService, scope.ServiceProvider.GetRequiredService<IRecordService>());

        var peopleService = scope.ServiceProvider.GetRequiredService<PeopleService>();
        Assert.Same(peopleService, scope.ServiceProvider.GetRequiredService<IPeopleService>());

        var profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();
        Assert.Same(profileService, scope.ServiceProvider.GetRequiredService<IProfileService>());

        var dictionaryService = scope.ServiceProvider.GetRequiredService<DictionaryService>();
        Assert.Same(dictionaryService, scope.ServiceProvider.GetRequiredService<IDictionaryService>());
    }

    [Fact]
    public void AddPmTrackerDataStore_ShouldNotRegisterLegacyUseCaseContracts()
    {
        var services = new ServiceCollection();
        services.AddPmTrackerDataStore(BuildConfiguration());

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProfilePageQueriesUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IMeetingListQueriesUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IMeetingDetailQueriesUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IMeetingWriteCommandsUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProjectListQueriesUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProjectCommandsUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProjectDetailQueriesUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProjectAssignmentCommandsUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProjectDataService");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IPeoplePageQueriesUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IPersonCommandsUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IDictionariesQueriesUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IDictionariesCommandsUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IRecordEditorQueriesUseCase");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IRecordWriteCommandsUseCase");
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
