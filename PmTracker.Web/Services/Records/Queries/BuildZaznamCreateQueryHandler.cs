using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Queries;

public sealed class BuildZaznamCreateQueryHandler(IRecordsDataStore dataStore) : IBuildZaznamCreateQueryHandler
{
    public ZaznamEditViewModel Handle(int projektId, int? jednaniId = null)
        => dataStore.BuildZaznamCreate(projektId, jednaniId);
}
