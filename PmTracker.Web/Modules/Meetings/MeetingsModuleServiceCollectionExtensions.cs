namespace PmTracker.Web.Modules.Meetings;

public static class MeetingsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddMeetingsModule(this IServiceCollection services)
    {
        services.AddScoped<IMeetingsQueries, MeetingsQueries>();
        services.AddScoped<IMeetingsCommands, MeetingsCommands>();
        return services;
    }
}
