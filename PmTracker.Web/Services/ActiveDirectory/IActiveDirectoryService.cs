namespace PmTracker.Web.Services.ActiveDirectory;

public interface IActiveDirectoryService
{
    Task<ActiveDirectorySearchResponse> SearchUsersAsync(string? query, CancellationToken ct = default);

    /// <summary>
    /// Načte osoby z AD podle seznamu GuidAd. Interně rozděluje na batches po 100.
    /// </summary>
    Task<ActiveDirectoryBatchResponse> ListByGuidsAsync(
        IReadOnlyCollection<Guid> guids,
        CancellationToken ct = default);
}
