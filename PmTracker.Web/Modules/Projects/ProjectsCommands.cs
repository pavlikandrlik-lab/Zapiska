using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Modules.Projects;

public sealed class ProjectsCommands(IPmTrackerDataStore dataStore) : IProjectsCommands
{
    public int SaveProject(SaveProjectCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveProject(command, currentUser);

    public void SoftDeleteProject(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SoftDeleteProject(command, currentUser);
}
