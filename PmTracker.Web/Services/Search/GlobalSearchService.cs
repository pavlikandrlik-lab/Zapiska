using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Search;

public sealed class GlobalSearchService : IGlobalSearchService
{
    private const int MinQueryLength = 2;
    private const int MaxQueryLength = 200;

    private readonly ISearchClient _client;
    private readonly IEmbeddingService _embeddingService;
    private readonly SearchOptions _options;
    private readonly ILogger<GlobalSearchService> _logger;

    public GlobalSearchService(
        ISearchClient client,
        IEmbeddingService embeddingService,
        IOptions<SearchOptions> options,
        ILogger<GlobalSearchService> logger)
    {
        _client = client;
        _embeddingService = embeddingService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GlobalSearchResult> SearchAsync(string query, CurrentUserContextViewModel currentUser, int pageSize, CancellationToken cancellationToken)
    {
        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length < MinQueryLength)
        {
            return new GlobalSearchResult { Query = trimmed };
        }
        if (trimmed.Length > MaxQueryLength)
        {
            trimmed = trimmed.Substring(0, MaxQueryLength);
        }

        var multiplier = Math.Max(1, _options.PostFilterCandidateMultiplier);
        var candidatesLimit = Math.Max(pageSize, pageSize * multiplier);

        IReadOnlyList<int>? preFilter = null;
        if (!currentUser.IsSuperAdmin)
        {
            preFilter = currentUser.VisibleProjectIds;
        }

        IReadOnlyList<float>? embedding = null;
        if (_options.Embeddings.Enabled)
        {
            embedding = await _embeddingService.EmbedAsync(trimmed, cancellationToken).ConfigureAwait(false);
        }

        var request = new SearchQueryRequest
        {
            Query = trimmed,
            QueryEmbedding = embedding,
            PreFilterProjectIds = preFilter,
            Size = candidatesLimit
        };

        SearchQueryResponse response;
        try
        {
            response = await _client.SearchAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Globální vyhledávání selhalo pro dotaz '{Query}'.", trimmed);
            return new GlobalSearchResult { Query = trimmed };
        }

        var filtered = new List<SearchHit>(pageSize);
        var hasMore = false;
        foreach (var hit in response.Hits)
        {
            if (!SearchAcl.IsAccessible(currentUser, hit))
            {
                continue;
            }
            if (filtered.Count >= pageSize)
            {
                hasMore = true;
                break;
            }
            filtered.Add(hit);
        }

        var groups = filtered
            .GroupBy(h => h.EntityType)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SearchHit>)g.ToList());

        return new GlobalSearchResult
        {
            Query = trimmed,
            Hits = filtered,
            Groups = groups,
            TotalCandidates = response.TotalCandidates,
            HasMore = hasMore
        };
    }
}
