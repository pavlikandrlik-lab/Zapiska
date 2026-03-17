using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Projects.Queries;

namespace PmTracker.Web.Modules.Projects;

public sealed class ProjectsQueries(
    IProjektExistsQueryHandler projektExistsQueryHandler,
    IBuildProjektyListQueryHandler buildProjektyListQueryHandler,
    IBuildProjektDetailQueryHandler buildProjektDetailQueryHandler,
    IBuildProjectStatusOptionsQueryHandler buildProjectStatusOptionsQueryHandler) : IProjectsQueries
{
    public bool ProjektExists(int id) => projektExistsQueryHandler.Handle(id);

    public IReadOnlyList<ProjektListItemViewModel> BuildProjektyList() => buildProjektyListQueryHandler.Handle();

    public ProjektDetailViewModel BuildProjektDetail(int id) => buildProjektDetailQueryHandler.Handle(id);

    public IReadOnlyList<LookupOptionViewModel> BuildProjectStatusOptions(CurrentUserContextViewModel currentUser)
        => buildProjectStatusOptionsQueryHandler.Handle(currentUser);
}
