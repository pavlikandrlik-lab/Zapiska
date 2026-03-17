using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Profile;

public interface IProfileDataStore
{
    ProfilPageViewModel BuildProfilPage(CurrentUserContextViewModel currentUser, int? projektId);
}
