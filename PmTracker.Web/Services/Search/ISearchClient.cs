namespace PmTracker.Web.Services.Search;

public interface ISearchClient
{
    Task EnsureIndexAsync(CancellationToken cancellationToken);
    Task BulkIndexAsync(IReadOnlyCollection<SearchDocument> documents, CancellationToken cancellationToken);
    Task DeleteDocumentAsync(string entityType, string entityId, CancellationToken cancellationToken);
    Task<SearchQueryResponse> SearchAsync(SearchQueryRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Celkový počet dokumentů v indexu (pro status panel v Nastavení + bootstrap detekci
    /// prázdného indexu při startu aplikace).
    /// </summary>
    Task<long> GetDocumentCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// True pokud je fulltextový index skutečně nakonfigurován a dotazovatelný.
    /// Pro SqlServer: existuje FTS katalog + FTS index na SearchIndex.
    /// Pro OpenSearch: ping/health check.
    /// </summary>
    Task<bool> IsSearchableAsync(CancellationToken cancellationToken);
}
