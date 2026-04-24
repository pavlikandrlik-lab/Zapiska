using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.ServiceDesk.Sql;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Dictionaries;
using PmTracker.Web.Services.People;
using PmTracker.Web.Services.Profile;

namespace PmTracker.Tests.Unit.Common;

public sealed class PmTrackerModuleServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPmTrackerDataStore_ShouldRegisterFacadeAliasesWithoutUseCaseRegistrations()
    {
        var services = new ServiceCollection();

        services.AddPmTrackerDataStore(BuildConfiguration());

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(MeetingService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ProjectService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(RecordService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(PeopleService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ProfileService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(DictionaryService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);

        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProfilePageQueriesUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IMeetingListQueriesUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IMeetingDetailQueriesUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IMeetingWriteCommandsUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProjectListQueriesUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProjectCommandsUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProjectDetailQueriesUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProjectAssignmentCommandsUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IProjectDataService");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IPeoplePageQueriesUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IPersonCommandsUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IDictionariesQueriesUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IDictionariesCommandsUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IRecordEditorQueriesUseCase");
        services.Should().NotContain(descriptor => descriptor.ServiceType.FullName == "PmTracker.Web.Services.Data.IRecordWriteCommandsUseCase");
    }

    [Fact]
    public void AddPmTrackerDataStore_ShouldResolveFacadeAliasesToSameScopedInstances()
    {
        var configuration = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddPmTrackerDataStore(configuration);
        // Plán 3 Feature D (2026-04-24): ExterniOdkazValidator registrovaný v
        // AddPmTrackerDataStore závisí na IVyjadreniQueryService, který registruje
        // až AddServiceDeskIntegration (v Program.cs volané hned za PmTrackerDataStore).
        // Pro DI smoke test musíme proto i tady zavolat obě registrace v pořadí
        // jako v Program.cs, jinak se RecordService neodvodí.
        services.AddServiceDeskIntegration(configuration);
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var meetingService = scope.ServiceProvider.GetRequiredService<MeetingService>();
        scope.ServiceProvider.GetRequiredService<IMeetingService>().Should().BeSameAs(meetingService);

        var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
        scope.ServiceProvider.GetRequiredService<IProjectService>().Should().BeSameAs(projectService);
        scope.ServiceProvider.GetRequiredService<IProjectDetailComposition>().Should().BeSameAs(projectService);
        scope.ServiceProvider.GetRequiredService<IRecordEditorQueriesComposition>().Should().BeSameAs(projectService);
        scope.ServiceProvider.GetRequiredService<IRecordWriteCommandsComposition>().Should().BeSameAs(projectService);

        var recordService = scope.ServiceProvider.GetRequiredService<RecordService>();
        scope.ServiceProvider.GetRequiredService<IRecordService>().Should().BeSameAs(recordService);

        var peopleService = scope.ServiceProvider.GetRequiredService<PeopleService>();
        scope.ServiceProvider.GetRequiredService<IPeopleService>().Should().BeSameAs(peopleService);

        var profileService = scope.ServiceProvider.GetRequiredService<ProfileService>();
        scope.ServiceProvider.GetRequiredService<IProfileService>().Should().BeSameAs(profileService);

        var dictionaryService = scope.ServiceProvider.GetRequiredService<DictionaryService>();
        scope.ServiceProvider.GetRequiredService<IDictionaryService>().Should().BeSameAs(dictionaryService);
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
