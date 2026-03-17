using PmTracker.Web.Modules.Projects.Commands;
using PmTracker.Web.Modules.Projects.Queries;

namespace PmTracker.Web.Modules.Projects;

public static class ProjectsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectsDataStore, ProjectsDataStore>();
        services.AddScoped<IProjektExistsQueryHandler, ProjektExistsQueryHandler>();
        services.AddScoped<IBuildProjektyListQueryHandler, BuildProjektyListQueryHandler>();
        services.AddScoped<IBuildProjektDetailQueryHandler, BuildProjektDetailQueryHandler>();
        services.AddScoped<IBuildProjectStatusOptionsQueryHandler, BuildProjectStatusOptionsQueryHandler>();
        services.AddScoped<IProjectsQueries, ProjectsQueries>();
        services.AddScoped<ISaveProjectCommandHandler, SaveProjectCommandHandler>();
        services.AddScoped<ISoftDeleteProjectCommandHandler, SoftDeleteProjectCommandHandler>();
        services.AddScoped<ISaveTeamMemberCommandHandler, SaveTeamMemberCommandHandler>();
        services.AddScoped<IRemoveTeamMemberCommandHandler, RemoveTeamMemberCommandHandler>();
        services.AddScoped<IAssignProjectRoleCommandHandler, AssignProjectRoleCommandHandler>();
        services.AddScoped<IDeactivateProjectRoleCommandHandler, DeactivateProjectRoleCommandHandler>();
        services.AddScoped<IAssignProjectSubsystemCommandHandler, AssignProjectSubsystemCommandHandler>();
        services.AddScoped<IDeactivateProjectSubsystemCommandHandler, DeactivateProjectSubsystemCommandHandler>();
        services.AddScoped<IAssignProjectSubsystemRoleCommandHandler, AssignProjectSubsystemRoleCommandHandler>();
        services.AddScoped<IDeactivateProjectSubsystemRoleCommandHandler, DeactivateProjectSubsystemRoleCommandHandler>();
        services.AddScoped<IProjectsCommands, ProjectsCommands>();
        return services;
    }
}
