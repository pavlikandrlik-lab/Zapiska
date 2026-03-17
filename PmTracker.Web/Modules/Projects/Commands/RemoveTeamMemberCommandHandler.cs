using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Commands;

public sealed class RemoveTeamMemberCommandHandler(IProjectsDataStore dataStore) : IRemoveTeamMemberCommandHandler
{
    public void Handle(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.RemoveTeamMember(command, currentUser);
}
