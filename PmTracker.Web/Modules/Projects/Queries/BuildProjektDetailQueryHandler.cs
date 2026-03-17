using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Queries;

public sealed class BuildProjektDetailQueryHandler(IProjectsDataStore dataStore) : IBuildProjektDetailQueryHandler
{
    public ProjektDetailViewModel Handle(int id)
        => dataStore.BuildProjektDetail(id);
}
