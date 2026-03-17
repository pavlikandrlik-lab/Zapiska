using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public sealed class DeactivateProjectRoleCommandHandler(IProjectsDataStore dataStore) : IDeactivateProjectRoleCommandHandler
{
    public void Handle(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.DeactivateProjectRole(command, currentUser);
}
