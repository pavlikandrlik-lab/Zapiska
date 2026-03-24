using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Profile;

public interface IProfileService
{
    Task<ProfilPageViewModel> BuildProfilPageAsync(CurrentUserContextViewModel currentUser, int? projektId, CancellationToken ct = default);
}
