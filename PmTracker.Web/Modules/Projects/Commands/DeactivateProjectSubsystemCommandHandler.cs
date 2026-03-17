using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public sealed class DeactivateProjectSubsystemCommandHandler(IProjectsDataStore dataStore) : IDeactivateProjectSubsystemCommandHandler
{
    public void Handle(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.DeactivateProjectSubsystem(command, currentUser);
}
