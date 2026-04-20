using Microsoft.Extensions.DependencyInjection;

namespace PmTracker.Web.Services.Vyzvy;

public static class VyzvyServiceCollectionExtensions
{
    public static IServiceCollection AddVyzvyServices(this IServiceCollection services)
    {
        services.AddScoped<IVyzvaService, VyzvaService>();
        return services;
    }
}
