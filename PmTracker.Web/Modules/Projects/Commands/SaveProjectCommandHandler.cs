using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public sealed class SaveProjectCommandHandler(IProjectsDataStore dataStore) : ISaveProjectCommandHandler
{
    public int Handle(SaveProjectCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveProject(command, currentUser);
}
