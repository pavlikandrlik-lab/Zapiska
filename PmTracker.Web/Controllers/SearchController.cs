using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PmTracker.Web.Models.ViewModels.Search;
using PmTracker.Web.Services.Search;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

// search.index drží všech 12 rolí (seed) — gatuje přístup k vyhledávání.
// Bez téhle policy by byl klíč mrtvý a hledání otevřené každému přihlášenému.
[Authorize(Policy = "permission:search.index")]
[EnableRateLimiting("search")]
public sealed class SearchController : BaseController
{
    private readonly IRecordSearchService _search;

    public SearchController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IRecordSearchService search)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _search = search;
    }

    /// <summary>Data pro dropdown pod vyhledávacím polem.</summary>
    [HttpGet]
    public async Task<IActionResult> Suggest(string? q, CancellationToken cancellationToken)
    {
        var result = await _search.SearchAsync(
            q, CurrentUserContext, RecordSearchService.DropdownLimit, cancellationToken);

        return Json(new
        {
            query = result.Query,
            totalCount = result.TotalCount,
            categories = result.Categories.Select(c => new
            {
                key = c.Key,
                nazev = c.Nazev,
                items = c.Items.Select(i => new
                {
                    zaznamId = i.ZaznamId,
                    nazev = i.Nazev,
                    cisloViditelne = i.CisloViditelne,
                    subsystemKod = i.SubsystemKod,
                    matchKind = i.MatchKind.ToString(),
                    snippet = i.Snippet is null ? null : new
                    {
                        before = i.Snippet.Before,
                        match = i.Snippet.Match,
                        after = i.Snippet.After
                    },
                    cisloJednani = i.CisloJednani,
                    detailUrl = i.DetailUrl
                })
            })
        });
    }

    /// <summary>Stránka výsledků.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var result = await _search.SearchAsync(
            q, CurrentUserContext, PageLimit, cancellationToken);

        SetSectionRootBreadcrumb("Hledání");

        return View(AttachCurrentUser(new SearchPageViewModel
        {
            Query = result.Query,
            Result = result
        }));
    }

    /// <summary>Kolik výsledků se načte na stránce výsledků.</summary>
    private const int PageLimit = 40;
}
