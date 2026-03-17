using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Export;

public interface IExportDataStore
{
    bool ProjektExists(int projektId);

    JednaniDetailViewModel BuildJednaniDetail(int jednaniId);
}
