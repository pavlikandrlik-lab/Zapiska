using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public sealed class SaveAttendanceCommandHandler(IMeetingsDataStore dataStore) : ISaveAttendanceCommandHandler
{
    public void Handle(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveAttendance(command, currentUser);
}
