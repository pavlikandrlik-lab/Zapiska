using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public sealed class SaveMeetingStatusCommandHandler(IMeetingsDataStore dataStore) : ISaveMeetingStatusCommandHandler
{
    public void Handle(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveMeetingStatus(command, currentUser);
}
