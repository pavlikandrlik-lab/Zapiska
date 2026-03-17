using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Queries;

public interface IBuildProjectStatusOptionsQueryHandler
{
    IReadOnlyList<LookupOptionViewModel> Handle(CurrentUserContextViewModel currentUser);
}
