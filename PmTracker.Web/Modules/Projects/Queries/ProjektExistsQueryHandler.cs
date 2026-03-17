namespace PmTracker.Web.Modules.Projects.Queries;

public sealed class ProjektExistsQueryHandler(IProjectsDataStore dataStore) : IProjektExistsQueryHandler
{
    public bool Handle(int id)
        => dataStore.ProjektExists(id);
}
