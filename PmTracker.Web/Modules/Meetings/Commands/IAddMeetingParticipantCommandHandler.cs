using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public interface IAddMeetingParticipantCommandHandler
{
    void Handle(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser);
}
