using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Search;

public interface IRecordSearchService
{
    /// <summary>
    /// Najde záznamy, které uživatel smí vidět a v nichž (nebo v jejich vyjádřeních
    /// či externích odkazech) se vyskytují všechna slova dotazu (krátká slova vedle delších se
    /// vynechají, viz <see cref="SearchQueryText.MinTermLength"/>).
    /// Dotaz kratší než <see cref="SearchQueryText.MinQueryLength"/> vrací prázdný výsledek.
    /// </summary>
    Task<SearchResult> SearchAsync(
        string? query,
        CurrentUserContextViewModel user,
        int limit,
        CancellationToken cancellationToken);
}
