using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IPersonCommandsUseCase
{
    int SaveManualPerson(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser);

    int SaveAdPerson(SaveAdPersonCommand command, CurrentUserContextViewModel currentUser);

    void DeletePerson(DeletePersonCommand command, CurrentUserContextViewModel currentUser);
}
