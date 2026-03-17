using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Profile;

public sealed class ProfileDataStore(IPmTrackerDataStore dataStore) : IProfileDataStore
{
    public ProfilPageViewModel BuildProfilPage(CurrentUserContextViewModel currentUser, int? projektId)
        => dataStore.BuildProfilPage(currentUser, projektId);
}
