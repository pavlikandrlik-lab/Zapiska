using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Modules.Meetings;

public sealed class MeetingsQueries(IPmTrackerDataStore dataStore) : IMeetingsQueries
{
    public bool ProjektExists(int id) => dataStore.ProjektExists(id);

    public ProjektDetailViewModel BuildProjektDetail(int id) => dataStore.BuildProjektDetail(id);

    public IReadOnlyList<JednaniProjektListItemViewModel> BuildJednaniOverview() => dataStore.BuildJednaniOverview();

    public JednaniDetailViewModel BuildJednaniDetail(int id) => dataStore.BuildJednaniDetail(id);
}
