using PmTracker.Web.Modules.Meetings.Commands;
using PmTracker.Web.Modules.Meetings.Queries;

namespace PmTracker.Web.Modules.Meetings;

public static class MeetingsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddMeetingsModule(this IServiceCollection services)
    {
        services.AddScoped<IMeetingsDataStore, MeetingsDataStore>();
        services.AddScoped<IProjektExistsQueryHandler, ProjektExistsQueryHandler>();
        services.AddScoped<IBuildProjektDetailQueryHandler, BuildProjektDetailQueryHandler>();
        services.AddScoped<IBuildJednaniOverviewQueryHandler, BuildJednaniOverviewQueryHandler>();
        services.AddScoped<IBuildJednaniDetailQueryHandler, BuildJednaniDetailQueryHandler>();
        services.AddScoped<IMeetingsQueries, MeetingsQueries>();

        services.AddScoped<ISaveMeetingCommandHandler, SaveMeetingCommandHandler>();
        services.AddScoped<IDeleteMeetingCommandHandler, DeleteMeetingCommandHandler>();
        services.AddScoped<ISaveMeetingStatusCommandHandler, SaveMeetingStatusCommandHandler>();
        services.AddScoped<ISaveMeetingNoteCommandHandler, SaveMeetingNoteCommandHandler>();
        services.AddScoped<ISaveAttendanceCommandHandler, SaveAttendanceCommandHandler>();
        services.AddScoped<IAddMeetingParticipantCommandHandler, AddMeetingParticipantCommandHandler>();
        services.AddScoped<IMeetingsCommands, MeetingsCommands>();
        return services;
    }
}
