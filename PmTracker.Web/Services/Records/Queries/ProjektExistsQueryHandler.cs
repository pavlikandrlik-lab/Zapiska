namespace PmTracker.Web.Services.Records.Queries;

public sealed class ProjektExistsQueryHandler(IRecordsDataStore dataStore) : IProjektExistsQueryHandler
{
    public bool Handle(int id)
        => dataStore.ProjektExists(id);
}
