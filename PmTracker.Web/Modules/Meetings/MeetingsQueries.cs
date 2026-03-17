using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Meetings.Queries;

namespace PmTracker.Web.Modules.Meetings;

public sealed class MeetingsQueries(
    IProjektExistsQueryHandler projektExistsQueryHandler,
    IBuildProjektDetailQueryHandler buildProjektDetailQueryHandler,
    IBuildJednaniOverviewQueryHandler buildJednaniOverviewQueryHandler,
    IBuildJednaniDetailQueryHandler buildJednaniDetailQueryHandler) : IMeetingsQueries
{
    public bool ProjektExists(int id) => projektExistsQueryHandler.Handle(id);

    public ProjektDetailViewModel BuildProjektDetail(int id) => buildProjektDetailQueryHandler.Handle(id);

    public IReadOnlyList<JednaniProjektListItemViewModel> BuildJednaniOverview() => buildJednaniOverviewQueryHandler.Handle();

    public JednaniDetailViewModel BuildJednaniDetail(int id) => buildJednaniDetailQueryHandler.Handle(id);
}
