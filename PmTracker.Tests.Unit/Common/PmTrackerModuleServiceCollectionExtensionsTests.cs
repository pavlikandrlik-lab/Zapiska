using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Web.Modules;
using PmTracker.Web.Modules.Export;
using PmTracker.Web.Modules.Export.Queries;
using PmTracker.Web.Modules.Meetings;
using PmTracker.Web.Modules.Projects;
using PmTracker.Web.Modules.Records;
using PmTracker.Web.Modules.Settings;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Export;
using PmTracker.Web.Services.Dictionaries;
using PmTracker.Web.Services.People;
using PmTracker.Web.Services.Profile;
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
            descriptor.ServiceType == typeof(IProjectsDataStore) &&
            descriptor.ImplementationType == typeof(ProjectsDataStore) &&
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
            descriptor.ServiceType == typeof(IMeetingsDataStore) &&
            descriptor.ImplementationType == typeof(MeetingsDataStore) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IPeopleService) &&
            descriptor.ImplementationType == typeof(PeopleService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IPeopleDataStore) &&
            descriptor.ImplementationType == typeof(PeopleDataStore) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IDictionariesService) &&
            descriptor.ImplementationType == typeof(DictionariesService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IDictionariesDataStore) &&
            descriptor.ImplementationType == typeof(DictionariesDataStore) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IProfileService) &&
            descriptor.ImplementationType == typeof(ProfileService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IProfileDataStore) &&
            descriptor.ImplementationType == typeof(ProfileDataStore) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ISettingsService) &&
            descriptor.ImplementationType == typeof(SettingsService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ISettingsDataStore) &&
            descriptor.ImplementationType == typeof(SettingsDataStore) &&
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
            descriptor.ServiceType == typeof(ISettingsModalModelFactory) &&
            descriptor.ImplementationType == typeof(SettingsModalModelFactory) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IWordExportService) &&
            descriptor.ImplementationType == typeof(OpenXmlWordExportService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IWordExportHeaderSectionWriter) &&
            descriptor.ImplementationType == typeof(OpenXmlWordHeaderSectionWriter) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IWordExportRichHtmlParagraphWriter) &&
            descriptor.ImplementationType == typeof(OpenXmlWordRichHtmlParagraphWriter) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IWordExportRecordHeaderWriter) &&
            descriptor.ImplementationType == typeof(OpenXmlWordRecordHeaderWriter) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IWordExportRecordCommentsCellWriter) &&
            descriptor.ImplementationType == typeof(OpenXmlWordRecordCommentsCellWriter) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IWordExportRecordPeopleCellWriter) &&
            descriptor.ImplementationType == typeof(OpenXmlWordRecordPeopleCellWriter) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IWordExportRecordDeadlinesCellWriter) &&
            descriptor.ImplementationType == typeof(OpenXmlWordRecordDeadlinesCellWriter) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IWordExportRecordsSectionWriter) &&
            descriptor.ImplementationType == typeof(OpenXmlWordRecordsSectionWriter) &&
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
            descriptor.ServiceType == typeof(IExportDataStore) &&
            descriptor.ImplementationType == typeof(ExportDataStore) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IExportProjektExistsQueryHandler) &&
            descriptor.ImplementationType == typeof(ExportProjektExistsQueryHandler) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IExportMeetingProjectIdQueryHandler) &&
            descriptor.ImplementationType == typeof(ExportMeetingProjectIdQueryHandler) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IExportCommentProjectionBuilder) &&
            descriptor.ImplementationType == typeof(ExportCommentProjectionBuilder) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IExportRoleProjectionBuilder) &&
            descriptor.ImplementationType == typeof(ExportRoleProjectionBuilder) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IExportAttendanceProjectionBuilder) &&
            descriptor.ImplementationType == typeof(ExportAttendanceProjectionBuilder) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IExportRecordVisibilityEvaluator) &&
            descriptor.ImplementationType == typeof(ExportRecordVisibilityEvaluator) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IExportRecordProjectionBuilder) &&
            descriptor.ImplementationType == typeof(ExportRecordProjectionBuilder) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IExportTemplateSummaryBuilder) &&
            descriptor.ImplementationType == typeof(ExportTemplateSummaryBuilder) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IExportQueries) &&
            descriptor.ImplementationType == typeof(ExportQueries) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IRecordUiFlowResolver) &&
            descriptor.ImplementationType == typeof(RecordUiFlowResolver) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IRecordsDataStore) &&
            descriptor.ImplementationType == typeof(RecordsDataStore) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IUserContextResolver) &&
            descriptor.ImplementationType == typeof(UserContextResolver) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IRecordCommentCommandsUseCase) &&
            descriptor.ImplementationType == typeof(RecordCommentCommandsUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IProfilePageQueriesUseCase) &&
            descriptor.ImplementationType == typeof(ProfilePageQueriesUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IMeetingListQueriesUseCase) &&
            descriptor.ImplementationType == typeof(MeetingListQueriesUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IMeetingDetailQueriesUseCase) &&
            descriptor.ImplementationType == typeof(MeetingDetailQueriesUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IMeetingWriteCommandsUseCase) &&
            descriptor.ImplementationType == typeof(MeetingWriteCommandsUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IProjectListQueriesUseCase) &&
            descriptor.ImplementationType == typeof(ProjectListQueriesUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IProjectCommandsUseCase) &&
            descriptor.ImplementationType == typeof(ProjectCommandsUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IProjectDetailQueriesUseCase) &&
            descriptor.ImplementationType == typeof(ProjectDetailQueriesUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IPeoplePageQueriesUseCase) &&
            descriptor.ImplementationType == typeof(PeoplePageQueriesUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IDictionariesQueriesUseCase) &&
            descriptor.ImplementationType == typeof(DictionariesQueriesUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IDictionariesCommandsUseCase) &&
            descriptor.ImplementationType == typeof(DictionariesCommandsUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IPersonCommandsUseCase) &&
            descriptor.ImplementationType == typeof(PersonCommandsUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IRecordEditorQueriesUseCase) &&
            descriptor.ImplementationType == typeof(RecordEditorQueriesUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IRecordWriteCommandsUseCase) &&
            descriptor.ImplementationType == typeof(RecordWriteCommandsUseCase) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IProjectAssignmentCommandsUseCase) &&
            descriptor.ImplementationType == typeof(ProjectAssignmentCommandsUseCase) &&
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
