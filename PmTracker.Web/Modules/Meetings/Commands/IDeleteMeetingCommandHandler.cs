using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public interface IDeleteMeetingCommandHandler
{
    void Handle(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser);
}
