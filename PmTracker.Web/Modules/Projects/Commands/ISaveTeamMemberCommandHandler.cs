using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public interface ISaveTeamMemberCommandHandler
{
    void Handle(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser);
}
