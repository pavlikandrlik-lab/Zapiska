using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Profile;

public sealed class ProfileService(IProfileDataStore dataStore) : IProfileService
{
    public ProfilPageViewModel BuildProfilPage(CurrentUserContextViewModel currentUser, int? projektId)
        => dataStore.BuildProfilPage(currentUser, projektId);
}
