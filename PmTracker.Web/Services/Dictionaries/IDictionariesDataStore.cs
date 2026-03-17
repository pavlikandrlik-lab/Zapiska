using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Dictionaries;

public interface IDictionariesDataStore
{
    CiselnikyDashboardViewModel BuildCiselnikyDashboard(string? id, CurrentUserContextViewModel currentUser);
    CiselnikDetailViewModel BuildCiselnikDetail(string id, CurrentUserContextViewModel currentUser);
    void SaveCiselnikRow(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser);
    void DeleteCiselnikRow(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser);
}
