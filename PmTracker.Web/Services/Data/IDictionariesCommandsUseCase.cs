using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IDictionariesCommandsUseCase
{
    void SaveCiselnikRow(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser, IDictionariesCommandsComposition composition);

    void DeleteCiselnikRow(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser, IDictionariesCommandsComposition composition);
}
