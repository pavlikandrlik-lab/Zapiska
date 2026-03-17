using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Queries;

public sealed class BuildProjektyListQueryHandler(IProjectsDataStore dataStore) : IBuildProjektyListQueryHandler
{
    public IReadOnlyList<ProjektListItemViewModel> Handle()
        => dataStore.BuildProjektyList();
}
