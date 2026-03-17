using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Queries;

public interface IBuildProjektyListQueryHandler
{
    IReadOnlyList<ProjektListItemViewModel> Handle();
}
