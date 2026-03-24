using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Dictionaries;

public interface IDictionaryService
{
    Task<CiselnikyDashboardViewModel> BuildCiselnikyDashboardAsync(string? id, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task<CiselnikDetailViewModel> BuildCiselnikDetailAsync(string id, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveCiselnikRowAsync(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeleteCiselnikRowAsync(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}
