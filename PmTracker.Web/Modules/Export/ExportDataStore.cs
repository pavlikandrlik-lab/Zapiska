using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Modules.Export;

public sealed class ExportDataStore(IPmTrackerDataStore dataStore) : IExportDataStore
{
    public bool ProjektExists(int projektId) => dataStore.ProjektExists(projektId);

    public JednaniDetailViewModel BuildJednaniDetail(int jednaniId) => dataStore.BuildJednaniDetail(jednaniId);
}
