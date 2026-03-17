using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.People;

public interface IPeopleDataStore
{
    OsobyIndexViewModel BuildOsoby();
    int SaveManualPerson(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser);
    int SaveAdPerson(SaveAdPersonCommand command, CurrentUserContextViewModel currentUser);
    void DeletePerson(DeletePersonCommand command, CurrentUserContextViewModel currentUser);
}
