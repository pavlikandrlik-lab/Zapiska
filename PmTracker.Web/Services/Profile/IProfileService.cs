using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Profile;

public interface IProfileService
{
    ProfilPageViewModel BuildProfilPage(CurrentUserContextViewModel currentUser, int? projektId);
}
