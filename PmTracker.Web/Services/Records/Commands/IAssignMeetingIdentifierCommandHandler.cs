using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public interface IAssignMeetingIdentifierCommandHandler
{
    void Handle(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser);
}
