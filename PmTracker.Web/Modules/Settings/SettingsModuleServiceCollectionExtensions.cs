using PmTracker.Web.Services.Settings;

namespace PmTracker.Web.Modules.Settings;

public static class SettingsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsModule(this IServiceCollection services)
    {
        services.AddScoped<IUserAuthorizationSnapshotBuilder, UserAuthorizationSnapshotBuilder>();
        services.AddScoped<ISettingsAuthzQueries, SettingsAuthzQueries>();
        services.AddScoped<ISettingsAuthzCommands, SettingsAuthzCommands>();
        services.AddScoped<ISettingsService, SettingsService>();
        return services;
    }
}
