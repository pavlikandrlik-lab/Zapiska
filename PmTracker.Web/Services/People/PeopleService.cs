using PmTracker.Web.Models.ViewModels;
namespace PmTracker.Web.Services.People;

public sealed class PeopleService(
    IPeopleDataStore dataStore) : IPeopleService
{
    public OsobyIndexViewModel BuildOsoby() => dataStore.BuildOsoby();

    public int SaveManualPerson(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser) =>
        dataStore.SaveManualPerson(command, currentUser);

    public int SaveAdPerson(SaveAdPersonCommand command, CurrentUserContextViewModel currentUser) =>
        dataStore.SaveAdPerson(command, currentUser);

    public void DeletePerson(DeletePersonCommand command, CurrentUserContextViewModel currentUser) =>
        dataStore.DeletePerson(command, currentUser);
}
