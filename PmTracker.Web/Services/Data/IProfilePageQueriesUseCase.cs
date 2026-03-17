using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IProfilePageQueriesUseCase
{
    ProfilPageViewModel BuildProfilPage(CurrentUserContextViewModel currentUser, int? projektId);
}
