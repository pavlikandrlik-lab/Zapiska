using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public interface ISaveMeetingStatusCommandHandler
{
    void Handle(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser);
}
