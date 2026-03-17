using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public sealed class AddMeetingParticipantCommandHandler(IMeetingsDataStore dataStore) : IAddMeetingParticipantCommandHandler
{
    public void Handle(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.AddMeetingParticipant(command, currentUser);
}
