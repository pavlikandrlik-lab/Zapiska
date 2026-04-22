using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Search;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

// M-3: Rate limit 30 req / 10s per user pro Index a Suggest — FREETEXTTABLE queries
// jsou drahé. Reindex/Status mají policy gating (search.reindex) — rate limit není
// potřeba, ale taky neškodí aplikovat celo-controller politiku.
[EnableRateLimiting("search")]
public sealed class SearchController : BaseController
{
    private readonly IGlobalSearchService _searchService;
    private readonly IDbSuggestService _dbSuggest;
    private readonly ISearchIndexer _indexer;
    private readonly SearchOptions _options;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IGlobalSearchService searchService,
        IDbSuggestService dbSuggest,
        ISearchIndexer indexer,
        IOptions<SearchOptions> options)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _searchService = searchService;
        _dbSuggest = dbSuggest;
        _indexer = indexer;
        _options = options.Value;
        _logger = loggerFactory.CreateLogger<SearchController>();
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var result = _options.Enabled && !string.IsNullOrWhiteSpace(q)
            ? await _searchService.SearchAsync(q, CurrentUserContext, pageSize: 40, cancellationToken)
            : new GlobalSearchResult { Query = q ?? string.Empty };

        return View(AttachCurrentUser(new GlobalSearchPageViewModel
        {
            Query = q ?? string.Empty,
            Result = result,
            SearchEnabled = _options.Enabled
        }));
    }

    [HttpGet]
    public async Task<IActionResult> Suggest(string? q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return Json(new { hits = Array.Empty<object>() });
        }

        var hits = await _dbSuggest.SuggestAsync(q, CurrentUserContext, limit: 10, cancellationToken);
        var payload = hits.Select(h => new
        {
            type = h.Type,
            title = h.Title,
            snippet = h.Snippet,
            url = h.Url,
            projektNazev = h.ProjektNazev
        });
        return Json(new { hits = payload });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:search.reindex")]
    public async Task<IActionResult> Reindex(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return BadRequest(new { error = "Vyhledávání je vypnuté." });
        }

        var count = await _indexer.FullReindexAsync(cancellationToken);
        return Json(new { indexed = count });
    }

    /// <summary>
    /// Status fulltextového indexu pro super-admin dashboard (Profil/Index).
    /// Viz inbox úprava #16 — UI tlačítko „Reindexovat vyhledávání".
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "permission:search.reindex")]
    public async Task<IActionResult> Status(
        [FromServices] ISearchClient client,
        CancellationToken cancellationToken)
    {
        var count = await client.GetDocumentCountAsync(cancellationToken);
        var searchable = await client.IsSearchableAsync(cancellationToken);
        return Json(new
        {
            enabled = _options.Enabled,
            provider = _options.Provider,
            isSearchable = searchable,
            documentCount = count,
        });
    }

    // Zaznamy nemají vlastní detail stránku — záznam se zobrazuje v detailu projektu.
    // Osoby nemají detail stránku.
    // Subsystémy jsou součástí číselníků.
    // ZaznamNavrh — ProposalDetail vyžaduje projektId + proposalId.
    private static string BuildDetailUrl(SearchHit h) => h.EntityType switch
    {
        EntityDocumentMapper.TypeProjekt => $"/Projekty/Detail/{h.EntityId}",
        EntityDocumentMapper.TypeZaznam => h.ProjektId.HasValue
            ? $"/Projekty/Detail/{h.ProjektId}?recordId={h.EntityId}"
            : "#",
        EntityDocumentMapper.TypeJednani => $"/Jednani/Detail/{h.EntityId}",
        EntityDocumentMapper.TypeOsoba => "/Osoby/Index",
        EntityDocumentMapper.TypeSubsystem => "/Ciselniky/Detail/subsystemy",
        EntityDocumentMapper.TypeVyjadreni => h.Meta.TryGetValue("zaznam_id", out var zid) && h.ProjektId.HasValue
            ? $"/Projekty/Detail/{h.ProjektId}?recordId={zid}"
            : "#",
        EntityDocumentMapper.TypeZaznamNavrh => h.ProjektId.HasValue
            ? $"/Navrhy/ProposalDetail?projektId={h.ProjektId}&proposalId={h.EntityId}"
            : "#",
        _ => "#"
    };
}
