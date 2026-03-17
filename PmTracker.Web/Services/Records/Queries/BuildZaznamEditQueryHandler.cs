using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Queries;

public sealed class BuildZaznamEditQueryHandler(IRecordsDataStore dataStore) : IBuildZaznamEditQueryHandler
{
    public ZaznamEditViewModel Handle(int id)
        => dataStore.BuildZaznamEdit(id);
}
