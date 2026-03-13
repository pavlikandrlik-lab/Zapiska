using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Web.Modules;
using PmTracker.Web.Modules.Export;
using PmTracker.Web.Modules.Meetings;
using PmTracker.Web.Modules.Projects;
using PmTracker.Web.Modules.Settings;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Export;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Settings;

namespace PmTracker.Tests.Unit.Common;

public sealed class PmTrackerModuleServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPmTrackerModules_ShouldRegisterModuleServicesOnTopOfExistingInfrastructure()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        services.AddPmTrackerDataStore(configuration);
        services.AddPmTrackerModules();

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IRecordsService) &&
            descriptor.ImplementationType == typeof(RecordsService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IProjectsQueries) &&
            descriptor.ImplementationType == typeof(ProjectsQueries) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IProjectsCommands) &&
            descriptor.ImplementationType == typeof(ProjectsCommands) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IMeetingsQueries) &&
            descriptor.ImplementationType == typeof(MeetingsQueries) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IMeetingsCommands) &&
            descriptor.ImplementationType == typeof(MeetingsCommands) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ISettingsService) &&
            descriptor.ImplementationType == typeof(SettingsService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IUserAuthorizationSnapshotBuilder) &&
            descriptor.ImplementationType == typeof(UserAuthorizationSnapshotBuilder) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ISettingsAuthzQueries) &&
            descriptor.ImplementationType == typeof(SettingsAuthzQueries) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ISettingsAuthzCommands) &&
            descriptor.ImplementationType == typeof(SettingsAuthzCommands) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IWordExportService) &&
            descriptor.ImplementationType == typeof(OpenXmlWordExportService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IExportTemplateQueries) &&
            descriptor.ImplementationType == typeof(ExportTemplateQueries) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IExportTemplateUseCase) &&
            descriptor.ImplementationType == typeof(ExportTemplateUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IUserContextResolver) &&
            descriptor.ImplementationType == typeof(UserContextResolver) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IPmTrackerDataStore) &&
            descriptor.ImplementationFactory != null &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PmTrackerDb"] = "Server=localhost;Database=PmTrackerTests;User Id=sa;Password=Password123!;TrustServerCertificate=True;",
                [$"{PmTrackerDataOptions.SectionName}:Provider"] = "SqlServer",
                [$"{PmTrackerDataOptions.SectionName}:SqlServer:ConnectionStringName"] = "PmTrackerDb",
                [$"{PmTrackerDataOptions.SectionName}:SqlServer:CommandTimeoutSeconds"] = "60",
                [$"{PmTracker.Web.Services.ActiveDirectory.ActiveDirectoryOptions.SectionName}:Domain"] = "acr",
                [$"{PmTracker.Web.Services.ActiveDirectory.ActiveDirectoryOptions.SectionName}:MaxResults"] = "15",
                [$"{PmTracker.Web.Services.ActiveDirectory.ActiveDirectoryOptions.SectionName}:QueryTimeoutSeconds"] = "8"
            })
            .Build();
    }
}
