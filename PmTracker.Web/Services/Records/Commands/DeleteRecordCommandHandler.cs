using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public sealed class DeleteRecordCommandHandler(IRecordsDataStore dataStore) : IDeleteRecordCommandHandler
{
    public void Handle(DeleteRecordCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.DeleteRecord(command, currentUser);
}
