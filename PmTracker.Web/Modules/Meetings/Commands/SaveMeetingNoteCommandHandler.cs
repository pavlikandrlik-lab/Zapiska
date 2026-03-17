using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public sealed class SaveMeetingNoteCommandHandler(IMeetingsDataStore dataStore) : ISaveMeetingNoteCommandHandler
{
    public void Handle(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveMeetingNote(command, currentUser);
}
