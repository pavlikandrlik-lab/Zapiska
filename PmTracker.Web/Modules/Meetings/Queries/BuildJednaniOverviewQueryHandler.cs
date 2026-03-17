using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Queries;

public sealed class BuildJednaniOverviewQueryHandler(IMeetingsDataStore dataStore) : IBuildJednaniOverviewQueryHandler
{
    public IReadOnlyList<JednaniProjektListItemViewModel> Handle()
        => dataStore.BuildJednaniOverview();
}
