using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Queries;

public sealed class BuildDeleteRecordModalQueryHandler(IRecordsDataStore dataStore) : IBuildDeleteRecordModalQueryHandler
{
    public DeleteRecordModalViewModel Handle(int projektId, int zaznamId)
        => dataStore.BuildDeleteRecordModal(projektId, zaznamId);
}
