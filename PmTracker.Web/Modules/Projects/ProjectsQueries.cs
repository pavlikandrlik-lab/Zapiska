using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Modules.Projects;

public sealed class ProjectsQueries(IPmTrackerDataStore dataStore) : IProjectsQueries
{
    public bool ProjektExists(int id) => dataStore.ProjektExists(id);

    public IReadOnlyList<ProjektListItemViewModel> BuildProjektyList() => dataStore.BuildProjektyList();

    public ProjektDetailViewModel BuildProjektDetail(int id) => dataStore.BuildProjektDetail(id);

    public IReadOnlyList<LookupOptionViewModel> BuildProjectStatusOptions(CurrentUserContextViewModel currentUser)
    {
        return dataStore.BuildCiselnikDetail("stavy-projektu", currentUser).Polozky
            .OrderBy(item => item.Nazev, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new LookupOptionViewModel
            {
                Value = item.Kod,
                Label = item.Nazev
            })
            .ToList();
    }
}
