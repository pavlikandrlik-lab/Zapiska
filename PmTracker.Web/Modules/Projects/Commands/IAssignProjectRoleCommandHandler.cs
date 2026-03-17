using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public interface IAssignProjectRoleCommandHandler
{
    void Handle(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser);
}
