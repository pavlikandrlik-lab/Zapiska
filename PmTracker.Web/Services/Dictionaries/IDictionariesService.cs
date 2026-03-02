using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Dictionaries;

public interface IDictionariesService
{
    CiselnikyDashboardViewModel BuildCiselnikyDashboard(string? id);
    CiselnikDetailViewModel BuildCiselnikDetail(string id);
    void SaveCiselnikRow(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser);
    void DeleteCiselnikRow(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser);
}
