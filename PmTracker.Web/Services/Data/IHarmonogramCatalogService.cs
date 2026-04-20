using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IHarmonogramCatalogService : IDictionariesQueriesComposition, IDictionariesCommandsComposition
{
    new Task<CiselnikDetailViewModel> BuildHarmonogramKrokyCiselnikDetailAsync(string key, bool canChangeLockState, CancellationToken ct = default);
    new Task<int> CountHarmonogramCatalogRowsAsync(CancellationToken ct = default);
    new Task<CiselnikDetailViewModel> BuildCiselnikDetailAsync(string id, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    new Task SaveHarmonogramStepRowAsync(SaveCiselnikRowCommand command, CancellationToken ct = default);
    new Task DeleteHarmonogramStepRowAsync(DeleteCiselnikRowCommand command, CancellationToken ct = default);
}
