using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IMeetingListQueriesUseCase
{
    IReadOnlyList<JednaniProjektListItemViewModel> BuildJednaniOverview();
    IReadOnlyList<JednaniListItemViewModel> BuildJednaniList(int projektId);
}
