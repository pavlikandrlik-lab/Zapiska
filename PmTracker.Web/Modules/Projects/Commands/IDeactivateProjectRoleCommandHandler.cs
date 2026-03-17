using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public interface IDeactivateProjectRoleCommandHandler
{
    void Handle(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser);
}
