using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public interface IRemoveTeamMemberCommandHandler
{
    void Handle(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser);
}
