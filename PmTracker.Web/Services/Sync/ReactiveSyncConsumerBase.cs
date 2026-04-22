using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PmTracker.Web.Services.Sync;

public abstract class ReactiveSyncConsumerBase<TRequest> : BackgroundService
    where TRequest : IHasDedupKey
{
    private readonly IReactiveSyncQueue<TRequest> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    protected ReactiveSyncConsumerBase(
        IReactiveSyncQueue<TRequest> queue,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected abstract Task HandleAsync(IServiceScope scope, TRequest request, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            var shouldBreak = false;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await HandleAsync(scope, request, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                shouldBreak = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Reactive sync consumer handler failed for request with key {DedupKey}",
                    request.DedupKey);
            }
            finally
            {
                _queue.AcknowledgeProcessed(request);
            }

            if (shouldBreak) break;
        }
    }
}
