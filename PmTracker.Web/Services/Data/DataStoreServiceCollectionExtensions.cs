using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PmTracker.Web.Data;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Dictionaries;
using PmTracker.Web.Services.People;
using PmTracker.Web.Services.Profile;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Documentation;

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
        services.AddScoped<IActiveDirectoryService, ActiveDirectoryService>();
        services.AddScoped<ITextNormalizer, TextNormalizer>();
        services.AddScoped<IPersonIdentityMatcher, PersonIdentityMatcher>();
        services.AddScoped<IPermissionEvaluationService, PermissionEvaluationService>();
        services.AddScoped<ICommentAuthorizationPolicy, CommentAuthorizationPolicy>();
        services.AddScoped<IRichTextContentService, RichTextContentService>();
        services.AddScoped<IProfilePageQueriesUseCase, ProfilePageQueriesUseCase>();
        services.AddScoped<IMeetingListQueriesUseCase, MeetingListQueriesUseCase>();
        services.AddScoped<IMeetingDetailQueriesUseCase, MeetingDetailQueriesUseCase>();
        services.AddScoped<IMeetingWriteCommandsUseCase, MeetingWriteCommandsUseCase>();
        services.AddScoped<IProjectListQueriesUseCase, ProjectListQueriesUseCase>();
        services.AddScoped<IProjectCommandsUseCase, ProjectCommandsUseCase>();
        services.AddScoped<IProjectDetailQueriesUseCase, ProjectDetailQueriesUseCase>();
        services.AddScoped<IPeoplePageQueriesUseCase, PeoplePageQueriesUseCase>();
        services.AddScoped<IDictionariesQueriesUseCase, DictionariesQueriesUseCase>();
        services.AddScoped<IDictionariesCommandsUseCase, DictionariesCommandsUseCase>();
        services.AddScoped<IPersonCommandsUseCase, PersonCommandsUseCase>();
        services.AddScoped<IRecordEditorQueriesUseCase, RecordEditorQueriesUseCase>();
        services.AddScoped<IRecordWriteCommandsUseCase, RecordWriteCommandsUseCase>();
        services.AddScoped<IProjectAssignmentCommandsUseCase, ProjectAssignmentCommandsUseCase>();
        services.AddScoped<IPeopleDataStore, PeopleDataStore>();
        services.AddScoped<IPeopleService, PeopleService>();
        services.AddScoped<IProfileDataStore, ProfileDataStore>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IDictionariesDataStore, DictionariesDataStore>();
        services.AddScoped<IDictionariesService, DictionariesService>();
        services.AddScoped<IDocumentationService, MarkdownDocumentationService>();
        services.AddScoped<IRecordCommentCommandsUseCase, RecordCommentCommandsUseCase>();
        services.AddScoped<SqlServerDataStore>();
        services.AddHostedService<SqlStartupValidatorHostedService>();
        services.AddScoped<IPmTrackerDataStore>(sp => sp.GetRequiredService<SqlServerDataStore>());

        return services;
    }
}
