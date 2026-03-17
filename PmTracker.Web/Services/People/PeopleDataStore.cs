using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.People;

public sealed class PeopleDataStore(IPmTrackerDataStore dataStore) : IPeopleDataStore
{
    public OsobyIndexViewModel BuildOsoby() => dataStore.BuildOsoby();

    public int SaveManualPerson(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveManualPerson(command, currentUser);

    public int SaveAdPerson(SaveAdPersonCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveAdPerson(command, currentUser);

    public void DeletePerson(DeletePersonCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.DeletePerson(command, currentUser);
}
