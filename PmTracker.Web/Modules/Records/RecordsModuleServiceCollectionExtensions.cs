using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Records.Commands;
using PmTracker.Web.Services.Records.Queries;

namespace PmTracker.Web.Modules.Records;

public static class RecordsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddRecordsModule(this IServiceCollection services)
    {
        services.AddScoped<IRecordsDataStore, RecordsDataStore>();
        services.AddScoped<IProjektExistsQueryHandler, ProjektExistsQueryHandler>();
        services.AddScoped<IBuildProjektDetailQueryHandler, BuildProjektDetailQueryHandler>();
        services.AddScoped<IBuildZaznamEditQueryHandler, BuildZaznamEditQueryHandler>();
        services.AddScoped<IBuildZaznamCreateQueryHandler, BuildZaznamCreateQueryHandler>();
        services.AddScoped<IBuildDeleteRecordModalQueryHandler, BuildDeleteRecordModalQueryHandler>();

        services.AddScoped<ISaveRecordCommandHandler, SaveRecordCommandHandler>();
        services.AddScoped<IDeleteRecordCommandHandler, DeleteRecordCommandHandler>();
        services.AddScoped<IAssignMeetingIdentifierCommandHandler, AssignMeetingIdentifierCommandHandler>();
        services.AddScoped<IAddCommentCommandHandler, AddCommentCommandHandler>();
        services.AddScoped<IUpdateCommentCommandHandler, UpdateCommentCommandHandler>();
        services.AddScoped<IDeleteCommentCommandHandler, DeleteCommentCommandHandler>();

        services.AddScoped<IRecordUiFlowResolver, RecordUiFlowResolver>();
        services.AddScoped<IRecordsService, RecordsService>();
        return services;
    }
}
