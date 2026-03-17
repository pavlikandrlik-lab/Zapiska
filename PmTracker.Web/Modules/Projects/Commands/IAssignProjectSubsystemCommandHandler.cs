using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public interface IAssignProjectSubsystemCommandHandler
{
    void Handle(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser);
}
