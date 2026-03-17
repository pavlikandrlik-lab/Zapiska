using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public interface ISaveAttendanceCommandHandler
{
    void Handle(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser);
}
