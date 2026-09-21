using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PmTracker.Web.Services.Search;

public static class SearchServiceCollectionExtensions
{
    public static IServiceCollection AddPmTrackerSearch(this IServiceCollection services, IConfiguration configuration)
    {
        // Vyhledávání běží nad ostrými tabulkami (spec 2026-09-17). Žádná indexová
        // vrstva, žádný externí klient, žádný reindex hosted service.
        services.AddScoped<IProjectVisibilityResolver, ProjectVisibilityResolver>();
        services.AddScoped<IRecordSearchService, RecordSearchService>();

        return services;
    }
}
