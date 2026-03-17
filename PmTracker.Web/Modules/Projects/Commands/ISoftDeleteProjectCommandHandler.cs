using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public interface ISoftDeleteProjectCommandHandler
{
    void Handle(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser);
}
