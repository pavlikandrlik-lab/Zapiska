using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public sealed class SaveTeamMemberCommandHandler(IProjectsDataStore dataStore) : ISaveTeamMemberCommandHandler
{
    public void Handle(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveTeamMember(command, currentUser);
}
