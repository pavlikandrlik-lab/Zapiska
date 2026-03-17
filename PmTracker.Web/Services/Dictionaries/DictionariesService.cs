using PmTracker.Web.Models.ViewModels;
namespace PmTracker.Web.Services.Dictionaries;

public sealed class DictionariesService(
    IDictionariesDataStore dataStore) : IDictionariesService
{
    public CiselnikyDashboardViewModel BuildCiselnikyDashboard(string? id, CurrentUserContextViewModel currentUser) => dataStore.BuildCiselnikyDashboard(id, currentUser);

    public CiselnikDetailViewModel BuildCiselnikDetail(string id, CurrentUserContextViewModel currentUser) => dataStore.BuildCiselnikDetail(id, currentUser);

    public void SaveCiselnikRow(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser) =>
        dataStore.SaveCiselnikRow(command, currentUser);

    public void DeleteCiselnikRow(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser) =>
        dataStore.DeleteCiselnikRow(command, currentUser);
}
