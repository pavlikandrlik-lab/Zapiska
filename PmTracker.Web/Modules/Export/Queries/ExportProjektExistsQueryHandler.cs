namespace PmTracker.Web.Modules.Export.Queries;

public sealed class ExportProjektExistsQueryHandler(IExportDataStore dataStore) : IExportProjektExistsQueryHandler
{
    public bool Handle(int projektId)
        => dataStore.ProjektExists(projektId);
}
