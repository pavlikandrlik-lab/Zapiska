using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public interface IDeactivateProjectSubsystemRoleCommandHandler
{
    void Handle(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser);
}
