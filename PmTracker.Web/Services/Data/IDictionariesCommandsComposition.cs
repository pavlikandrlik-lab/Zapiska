using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IDictionariesCommandsComposition
{
    Task<CiselnikDetailViewModel> BuildCiselnikDetailAsync(string id, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}
