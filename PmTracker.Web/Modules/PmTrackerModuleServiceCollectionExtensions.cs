using PmTracker.Web.Modules.Export;
using PmTracker.Web.Modules.Meetings;
using PmTracker.Web.Modules.Projects;
using PmTracker.Web.Modules.Records;
using PmTracker.Web.Modules.Settings;

namespace PmTracker.Web.Modules;

public static class PmTrackerModuleServiceCollectionExtensions
{
    public static IServiceCollection AddPmTrackerModules(this IServiceCollection services)
    {
        services
            .AddProjectsModule()
            .AddMeetingsModule()
            .AddRecordsModule()
            .AddSettingsModule()
            .AddExportModule();

        return services;
    }
}
