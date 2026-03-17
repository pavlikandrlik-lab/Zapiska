using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public sealed class AssignProjectSubsystemRoleCommandHandler(IProjectsDataStore dataStore) : IAssignProjectSubsystemRoleCommandHandler
{
    public void Handle(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.AssignProjectSubsystemRole(command, currentUser);
}
