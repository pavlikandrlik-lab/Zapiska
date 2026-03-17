using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public sealed class SaveMeetingCommandHandler(IMeetingsDataStore dataStore) : ISaveMeetingCommandHandler
{
    public int Handle(SaveMeetingCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveMeeting(command, currentUser);
}
