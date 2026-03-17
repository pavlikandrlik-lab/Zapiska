using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public interface IDeactivateProjectSubsystemCommandHandler
{
    void Handle(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser);
}
