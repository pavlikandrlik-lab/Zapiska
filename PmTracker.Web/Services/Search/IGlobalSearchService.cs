using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Search;

public interface IGlobalSearchService
{
    Task<GlobalSearchResult> SearchAsync(string query, CurrentUserContextViewModel currentUser, int pageSize, CancellationToken cancellationToken);
}
