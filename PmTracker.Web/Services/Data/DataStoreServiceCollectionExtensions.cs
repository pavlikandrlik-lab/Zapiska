using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PmTracker.Web.Data;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Dictionaries;
using PmTracker.Web.Services.Export;
using PmTracker.Web.Services.Export.Queries;
using PmTracker.Web.Services.People;
using PmTracker.Web.Services.Profile;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Documentation;
using PmTracker.Web.Services.Dashboard;
using PmTracker.Web.Services.Home;
using PmTracker.Web.Services.Settings;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services.Data;

public static class DataStoreServiceCollectionExtensions
{
    public static IServiceCollection AddPmTrackerDataStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.Configure<PmTrackerDataOptions>(configuration.GetSection(PmTrackerDataOptions.SectionName));
        services.Configure<ActiveDirectoryOptions>(configuration.GetSection(ActiveDirectoryOptions.SectionName));
        services.AddDbContext<PmTrackerDbContext>((sp, optionsBuilder) =>
        {
            var dataOptions = sp.GetRequiredService<IOptions<PmTrackerDataOptions>>().Value;
            var provider = (dataOptions.Provider ?? "SqlServer").Trim();
            if (!string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Podporovaný provider je pouze SqlServer. Aktuální hodnota: '{dataOptions.Provider}'.");
            }

            var connectionStringName = dataOptions.SqlServer.ConnectionStringName;
            var connectionString = configuration.GetConnectionString(connectionStringName);

            optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(dataOptions.SqlServer.CommandTimeoutSeconds);
            });
        });

        services.AddScoped<IUserContextResolver, UserContextResolver>();
        services.AddScoped<PermissionSeeder>();
        services.AddScoped<IActiveDirectoryService, ActiveDirectoryService>();
        services.AddScoped<ITextNormalizer, TextNormalizer>();
        services.AddScoped<IPersonIdentityMatcher, PersonIdentityMatcher>();
        services.AddScoped<IPermissionEvaluationService, PermissionEvaluationService>();
        services.AddScoped<ICommentAuthorizationPolicy, CommentAuthorizationPolicy>();
        services.AddScoped<IAuditWriteService, AuditWriteService>();
        services.AddScoped<IRichTextContentService, RichTextContentService>();
        services.AddScoped<HarmonogramService>();
        services.AddScoped<CommentService>();
        services.AddScoped<MeetingService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<RecordService>();
        services.AddScoped<RecordProposalService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<HomeDashboardService>();
        services.AddScoped<PeopleService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<DictionaryService>();
        services.AddScoped<IHarmonogramService>(sp => sp.GetRequiredService<HarmonogramService>());
        services.AddScoped<ICommentService>(sp => sp.GetRequiredService<CommentService>());
        services.AddScoped<IMeetingService>(sp => sp.GetRequiredService<MeetingService>());
        services.AddScoped<IProjectService>(sp => sp.GetRequiredService<ProjectService>());
        services.AddScoped<IRecordService>(sp => sp.GetRequiredService<RecordService>());
        services.AddScoped<IRecordProposalService>(sp => sp.GetRequiredService<RecordProposalService>());
        services.AddScoped<IDashboardService>(sp => sp.GetRequiredService<DashboardService>());
        services.AddScoped<IHomeDashboardService>(sp => sp.GetRequiredService<HomeDashboardService>());
        services.AddScoped<IPeopleService>(sp => sp.GetRequiredService<PeopleService>());
        services.AddScoped<IProfileService>(sp => sp.GetRequiredService<ProfileService>());
        services.AddScoped<IDictionaryService>(sp => sp.GetRequiredService<DictionaryService>());
        services.AddScoped<IProjectDetailComposition>(sp => sp.GetRequiredService<ProjectService>());
        services.AddScoped<IRecordEditorQueriesComposition>(sp => sp.GetRequiredService<ProjectService>());
        services.AddScoped<IRecordWriteCommandsComposition>(sp => sp.GetRequiredService<ProjectService>());
        services.AddScoped<IDocumentationService, MarkdownDocumentationService>();
        services.AddScoped<IRecordUiFlowResolver, RecordUiFlowResolver>();
        services.AddScoped<IRecordProposalAuthorizationPolicy, RecordProposalAuthorizationPolicy>();
        services.AddScoped<IPendingScheduleProposalLockEvaluator, PendingScheduleProposalLockEvaluator>();
        services.AddScoped<RecordProposalPayloadMapper>();
        services.AddScoped<IExportCommentProjectionBuilder, ExportCommentProjectionBuilder>();
        services.AddScoped<IExportRoleProjectionBuilder, ExportRoleProjectionBuilder>();
        services.AddScoped<IExportAttendanceProjectionBuilder, ExportAttendanceProjectionBuilder>();
        services.AddScoped<IExportRecordVisibilityEvaluator, ExportRecordVisibilityEvaluator>();
        services.AddScoped<IExportRecordProjectionBuilder, ExportRecordProjectionBuilder>();
        services.AddScoped<IExportTemplateSummaryBuilder, ExportTemplateSummaryBuilder>();
        services.AddScoped<IExportTemplateQueries, ExportTemplateQueries>();
        services.AddScoped<IExportTemplateUseCase, ExportTemplateUseCase>();
        services.AddScoped<IWordExportService, OpenXmlWordExportService>();
        services.AddScoped<IUserAuthorizationSnapshotBuilder, UserAuthorizationSnapshotBuilder>();
        services.AddScoped<ISettingsAuthzQueries, SettingsAuthzQueries>();
        services.AddScoped<ISettingsAuthzCommands, SettingsAuthzCommands>();
        services.AddScoped<ISettingsModalModelFactory, SettingsModalModelFactory>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddHostedService<SqlStartupValidatorHostedService>();

        return services;
    }
}
