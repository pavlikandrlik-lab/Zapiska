using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.People;

public sealed class PeopleService : IPeopleService
{
    private readonly IPmTrackerDataStore _dataStore;

    public PeopleService(IPmTrackerDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public OsobyIndexViewModel BuildOsoby() => _dataStore.BuildOsoby();

    public int SaveManualPerson(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.SaveManualPerson(command, currentUser);

    public int SaveAdPerson(SaveAdPersonCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.SaveAdPerson(command, currentUser);

    public void DeletePerson(DeletePersonCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.DeletePerson(command, currentUser);
}
