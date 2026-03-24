using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IDictionariesQueriesComposition
{
    Task<CiselnikDetailViewModel> BuildHarmonogramKrokyCiselnikDetailAsync(string key, bool canChangeLockState, CancellationToken ct = default);

    Task<int> CountHarmonogramCatalogRowsAsync(CancellationToken ct = default);
}
