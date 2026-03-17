using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public sealed class AssignProjectRoleCommandHandler(IProjectsDataStore dataStore) : IAssignProjectRoleCommandHandler
{
    public void Handle(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.AssignProjectRole(command, currentUser);
}
