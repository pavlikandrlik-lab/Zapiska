using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Queries;

public sealed class BuildProjectStatusOptionsQueryHandler(IProjectsDataStore dataStore) : IBuildProjectStatusOptionsQueryHandler
{
    public IReadOnlyList<LookupOptionViewModel> Handle(CurrentUserContextViewModel currentUser)
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
