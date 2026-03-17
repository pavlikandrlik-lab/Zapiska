using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public sealed class SaveRecordCommandHandler(IRecordsDataStore dataStore) : ISaveRecordCommandHandler
{
    public int Handle(SaveRecordCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveRecord(command, currentUser);
}
