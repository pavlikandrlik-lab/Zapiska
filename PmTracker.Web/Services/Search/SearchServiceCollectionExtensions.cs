using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PmTracker.Web.Services.Search;

public static class SearchServiceCollectionExtensions
{
    public static IServiceCollection AddPmTrackerSearch(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SearchOptions>(configuration.GetSection(SearchOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);

        var searchSection = configuration.GetSection(SearchOptions.SectionName);
        var provider = searchSection.GetValue<string>("Provider") ?? "OpenSearch";

        if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ISearchClient, SqlServerSearchClient>();
        }
        else
        {
            services.AddHttpClient<ISearchClient, OpenSearchClient>();
        }

        services.AddHttpClient<AzureOpenAIEmbeddingService>();

        services.AddSingleton<NullEmbeddingService>();
        services.AddSingleton<IEmbeddingService>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SearchOptions>>().Value;
            if (opts.Embeddings.Enabled && string.Equals(opts.Embeddings.Provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<AzureOpenAIEmbeddingService>();
            }
            return sp.GetRequiredService<NullEmbeddingService>();
        });

        services.AddScoped<ISearchIndexer, SearchIndexer>();
        services.AddScoped<IGlobalSearchService, GlobalSearchService>();
        services.AddHostedService<SearchReindexHostedService>();

        return services;
    }
}
