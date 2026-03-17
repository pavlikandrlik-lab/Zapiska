using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public sealed class AssignMeetingIdentifierCommandHandler(IRecordsDataStore dataStore) : IAssignMeetingIdentifierCommandHandler
{
    public void Handle(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.AssignMeetingIdentifier(command, currentUser);
}
