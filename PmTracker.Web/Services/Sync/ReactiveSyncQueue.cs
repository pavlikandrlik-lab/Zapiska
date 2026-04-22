using System.Threading.Channels;

namespace PmTracker.Web.Services.Sync;

public sealed class ReactiveSyncQueue<TRequest> : IReactiveSyncQueue<TRequest>
    where TRequest : IHasDedupKey
{
    private const int DefaultCapacity = 1000;

    private readonly Channel<TRequest> _channel;
    private readonly object _lock = new();
    private readonly HashSet<object> _pending = new();

    public ReactiveSyncQueue()
    {
        _channel = Channel.CreateBounded<TRequest>(new BoundedChannelOptions(DefaultCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async ValueTask EnqueueAsync(TRequest request, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_pending.Add(request.DedupKey))
            {
                return;  // already pending
            }
        }

        try
        {
            await _channel.Writer.WriteAsync(request, ct).ConfigureAwait(false);
        }
        catch
        {
            // Rollback dedup set pokud WriteAsync failne (cancellation, channel closed)
            lock (_lock) _pending.Remove(request.DedupKey);
            throw;
        }
    }

    public void AcknowledgeProcessed(TRequest request)
    {
        lock (_lock) _pending.Remove(request.DedupKey);
    }

    public IAsyncEnumerable<TRequest> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);

    public int PendingCount
    {
        get { lock (_lock) return _pending.Count; }
    }
}
