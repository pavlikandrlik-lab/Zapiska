using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IDictionariesQueriesUseCase
{
    CiselnikyDashboardViewModel BuildCiselnikyDashboard(string? id, CurrentUserContextViewModel currentUser, IDictionariesQueriesComposition composition);

    CiselnikDetailViewModel BuildCiselnikDetail(string id, CurrentUserContextViewModel currentUser, IDictionariesQueriesComposition composition);
}
