using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public interface ISaveProjectCommandHandler
{
    int Handle(SaveProjectCommand command, CurrentUserContextViewModel currentUser);
}
