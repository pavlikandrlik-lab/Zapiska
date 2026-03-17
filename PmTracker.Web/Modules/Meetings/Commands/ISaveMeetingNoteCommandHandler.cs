using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public interface ISaveMeetingNoteCommandHandler
{
    void Handle(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser);
}
