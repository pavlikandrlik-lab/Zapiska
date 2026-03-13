namespace PmTracker.Web.Modules.Projects;

public static class ProjectsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectsQueries, ProjectsQueries>();
        services.AddScoped<IProjectsCommands, ProjectsCommands>();
        return services;
    }
}
