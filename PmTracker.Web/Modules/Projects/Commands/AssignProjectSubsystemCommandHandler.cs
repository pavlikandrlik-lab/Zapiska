using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public sealed class AssignProjectSubsystemCommandHandler(IProjectsDataStore dataStore) : IAssignProjectSubsystemCommandHandler
{
    public void Handle(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.AssignProjectSubsystem(command, currentUser);
}
