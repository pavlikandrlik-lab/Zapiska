using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class AdReactiveSyncConsumer : ReactiveSyncConsumerBase<AdReactiveSyncRequest>
{
    public AdReactiveSyncConsumer(
        IReactiveSyncQueue<AdReactiveSyncRequest> queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AdReactiveSyncConsumer> logger)
        : base(queue, scopeFactory, logger) { }

    protected override async Task HandleAsync(IServiceScope scope, AdReactiveSyncRequest request, CancellationToken ct)
    {
        var svc = scope.ServiceProvider.GetRequiredService<IAdSyncService>();
        var result = await svc.SyncSinglePersonAsync(request.OsobaId, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AdReactiveSyncConsumer>>();
            logger.LogInformation(
                "AD reactive sync osoba={OsobaId} source={Source} failed: {Reason}",
                request.OsobaId, request.Source, result.FailureReason);
        }
    }
}
