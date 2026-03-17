using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IDictionariesQueriesComposition
{
    CiselnikDetailViewModel BuildHarmonogramKrokyCiselnikDetail(string key, bool canChangeLockState);

    int CountHarmonogramCatalogRows();
}
