using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Queries;

public sealed class ProjektExistsQueryHandler(IMeetingsDataStore dataStore) : IProjektExistsQueryHandler
{
    public bool Handle(int id)
        => dataStore.ProjektExists(id);
}
