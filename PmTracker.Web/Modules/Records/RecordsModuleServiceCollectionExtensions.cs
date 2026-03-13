using PmTracker.Web.Services.Records;

namespace PmTracker.Web.Modules.Records;

public static class RecordsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddRecordsModule(this IServiceCollection services)
    {
        services.AddScoped<IRecordsService, RecordsService>();
        return services;
    }
}
