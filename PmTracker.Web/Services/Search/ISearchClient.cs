namespace PmTracker.Web.Services.Search;

public interface ISearchClient
{
    Task EnsureIndexAsync(CancellationToken cancellationToken);
    Task BulkIndexAsync(IReadOnlyCollection<SearchDocument> documents, CancellationToken cancellationToken);
    Task DeleteDocumentAsync(string entityType, string entityId, CancellationToken cancellationToken);
    Task<SearchQueryResponse> SearchAsync(SearchQueryRequest request, CancellationToken cancellationToken);
}
