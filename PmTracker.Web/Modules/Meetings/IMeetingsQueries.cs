using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings;

public interface IMeetingsQueries
{
    bool ProjektExists(int id);
    ProjektDetailViewModel BuildProjektDetail(int id);
    IReadOnlyList<JednaniProjektListItemViewModel> BuildJednaniOverview();
    JednaniDetailViewModel BuildJednaniDetail(int id);
}
