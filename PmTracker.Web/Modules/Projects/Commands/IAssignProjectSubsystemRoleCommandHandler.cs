using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public interface IAssignProjectSubsystemRoleCommandHandler
{
    void Handle(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser);
}
