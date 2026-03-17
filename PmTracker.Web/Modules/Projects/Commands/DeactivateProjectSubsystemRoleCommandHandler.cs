using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public sealed class DeactivateProjectSubsystemRoleCommandHandler(IProjectsDataStore dataStore) : IDeactivateProjectSubsystemRoleCommandHandler
{
    public void Handle(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.DeactivateProjectSubsystemRole(command, currentUser);
}
