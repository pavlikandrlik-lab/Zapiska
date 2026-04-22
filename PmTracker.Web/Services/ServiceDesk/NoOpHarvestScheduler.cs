namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Placeholder implementace — call-site pro Plán B, nic neharvestuje.
/// Plán sd-sync-revise nahradí ReactiveHarvestSchedulerAdapter (zápis
/// do IReactiveSyncQueue&lt;SdReactiveHarvestRequest&gt;).
/// </summary>
internal sealed class NoOpHarvestScheduler : IHarvestScheduler
{
    public Task ScheduleHarvestAsync(int externiOdkazId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task ScheduleHarvestForRecordAsync(int zaznamId, CancellationToken ct = default)
        => Task.CompletedTask;
}
