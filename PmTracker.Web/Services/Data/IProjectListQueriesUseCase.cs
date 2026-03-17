using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IProjectListQueriesUseCase
{
    IReadOnlyList<ProjektListItemViewModel> BuildProjektyList();
}
