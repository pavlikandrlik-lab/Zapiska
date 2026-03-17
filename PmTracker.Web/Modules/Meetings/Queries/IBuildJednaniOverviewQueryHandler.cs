using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Queries;

public interface IBuildJednaniOverviewQueryHandler
{
    IReadOnlyList<JednaniProjektListItemViewModel> Handle();
}
