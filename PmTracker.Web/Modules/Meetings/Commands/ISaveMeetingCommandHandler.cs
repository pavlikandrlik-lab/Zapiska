using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public interface ISaveMeetingCommandHandler
{
    int Handle(SaveMeetingCommand command, CurrentUserContextViewModel currentUser);
}
