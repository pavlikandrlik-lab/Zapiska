namespace PmTracker.Web.Services.Sync;

public interface IReactiveSyncQueue<TRequest>
    where TRequest : IHasDedupKey
{
    /// <summary>Zapíše request do queue. Pokud už je stejný DedupKey pending, no-op.</summary>
    ValueTask EnqueueAsync(TRequest request, CancellationToken ct = default);

    /// <summary>Consumer volá po zpracování, aby uvolnil dedup slot.</summary>
    void AcknowledgeProcessed(TRequest request);

    IAsyncEnumerable<TRequest> ReadAllAsync(CancellationToken ct = default);

    int PendingCount { get; }
}
