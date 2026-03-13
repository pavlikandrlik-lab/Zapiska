using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PmTracker.Web.Data;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Dictionaries;
using PmTracker.Web.Services.People;
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
        services.AddScoped<IPeopleService, PeopleService>();
        services.AddScoped<IDictionariesService, DictionariesService>();
        services.AddScoped<IDocumentationService, MarkdownDocumentationService>();
        services.AddScoped<SqlServerDataStore>();
        services.AddHostedService<SqlStartupValidatorHostedService>();
        services.AddScoped<IPmTrackerDataStore>(sp => sp.GetRequiredService<SqlServerDataStore>());

        return services;
    }
}
