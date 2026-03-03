using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Dictionaries;

public sealed class DictionariesService : IDictionariesService
{
    private readonly IPmTrackerDataStore _dataStore;

    public DictionariesService(IPmTrackerDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public CiselnikyDashboardViewModel BuildCiselnikyDashboard(string? id, CurrentUserContextViewModel currentUser) => _dataStore.BuildCiselnikyDashboard(id, currentUser);

    public CiselnikDetailViewModel BuildCiselnikDetail(string id, CurrentUserContextViewModel currentUser) => _dataStore.BuildCiselnikDetail(id, currentUser);

    public void SaveCiselnikRow(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.SaveCiselnikRow(command, currentUser);

    public void DeleteCiselnikRow(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.DeleteCiselnikRow(command, currentUser);
}
