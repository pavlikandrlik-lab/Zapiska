using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Consumer sdílené reactive queue pro SD triggery T2/T5/T7/T8. Pro každý
/// request volá <see cref="IVyjadreniHarvestService.HarvestSingleTicketAsync"/>,
/// čímž využije fingerprint detekci (spec §5.2) — pokud se v HOT nic nezměnilo,
/// drill se přeskočí.
///
/// Dedup v queue: <see cref="SdReactiveHarvestRequest.DedupKey"/> = ExterniOdkazId —
/// opakovaný enqueue téhož odkazu ve stejném okamžiku je no-op dokud předchozí
/// request consumer nezpracuje (spec §13.4).
/// </summary>
public sealed class SdReactiveSyncConsumer : ReactiveSyncConsumerBase<SdReactiveHarvestRequest>
{
    public SdReactiveSyncConsumer(
        IReactiveSyncQueue<SdReactiveHarvestRequest> queue,
        IServiceScopeFactory scopeFactory,
        ILogger<SdReactiveSyncConsumer> logger)
        : base(queue, scopeFactory, logger) { }

    protected override async Task HandleAsync(
        IServiceScope scope,
        SdReactiveHarvestRequest request,
        CancellationToken ct)
    {
        var harvest = scope.ServiceProvider.GetRequiredService<IVyjadreniHarvestService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SdReactiveSyncConsumer>>();

        var result = await harvest.HarvestSingleTicketAsync(request.ExterniOdkazId, ct).ConfigureAwait(false);

        if (result.Message is not null)
        {
            logger.LogDebug(
                "SD reactive harvest externiOdkazId={Id} source={Source} → {Message}",
                request.ExterniOdkazId, request.Source, result.Message);
        }
        else
        {
            logger.LogInformation(
                "SD reactive harvest externiOdkazId={Id} source={Source} fetched={Fetched} created={Created} superseded={Superseded}",
                request.ExterniOdkazId, request.Source, result.Fetched, result.Created, result.Superseded);
        }
    }
}
