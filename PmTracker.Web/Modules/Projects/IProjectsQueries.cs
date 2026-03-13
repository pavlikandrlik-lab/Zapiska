using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects;

public interface IProjectsQueries
{
    bool ProjektExists(int id);
    IReadOnlyList<ProjektListItemViewModel> BuildProjektyList();
    ProjektDetailViewModel BuildProjektDetail(int id);
    IReadOnlyList<LookupOptionViewModel> BuildProjectStatusOptions(CurrentUserContextViewModel currentUser);
}
