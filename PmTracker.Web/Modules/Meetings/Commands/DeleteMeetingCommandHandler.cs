using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Commands;

public sealed class DeleteMeetingCommandHandler(IMeetingsDataStore dataStore) : IDeleteMeetingCommandHandler
{
    public void Handle(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.DeleteMeeting(command, currentUser);
}
