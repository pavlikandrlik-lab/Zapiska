using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public sealed class SoftDeleteProjectCommandHandler(IProjectsDataStore dataStore) : ISoftDeleteProjectCommandHandler
{
    public void Handle(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SoftDeleteProject(command, currentUser);
}
