namespace PmTracker.Web.Services.Search;

public interface ISearchIndexer
{
    Task IndexEntityAsync(string entityType, string entityId, CancellationToken cancellationToken);
    Task DeleteEntityAsync(string entityType, string entityId, CancellationToken cancellationToken);
    Task<int> FullReindexAsync(CancellationToken cancellationToken);
}
