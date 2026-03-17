using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IDictionariesCommandsComposition
{
    CiselnikDetailViewModel BuildCiselnikDetail(string id, CurrentUserContextViewModel currentUser);

    void SaveHarmonogramStepRow(SaveCiselnikRowCommand command);

    void DeleteHarmonogramStepRow(DeleteCiselnikRowCommand command);
}
