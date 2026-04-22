using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Periodic hosted service pro scope = aktivní tickety (HOT_ZAZNAMY.stav &lt;&gt; 'archiv').
/// Default perioda 60 min. Orchestrace (loop, semafor, LastResultJson) je v base třídě
/// <see cref="SyncHostedServiceBase{TSettings}"/> — tento třída jen dodává JobKey,
/// Load/Save settings a RunOnce delegaci.
/// Spec §4.
/// </summary>
public sealed class SdActivePeriodicSyncHostedService : SyncHostedServiceBase<SdActiveSyncSettingsEntity>
{
    public SdActivePeriodicSyncHostedService(
        IServiceScopeFactory scopeFactory,
        ISyncJobRunLock<SdActiveSyncSettingsEntity> runLock,
        ManualTriggerSignal<SdActiveSyncSettingsEntity> manualSignal,
        TimeProvider timeProvider,
        ILogger<SdActivePeriodicSyncHostedService> logger)
        : base(scopeFactory, runLock, manualSignal, timeProvider, logger) { }

    protected override string JobKey => "sd.active";

    protected override async Task<SdActiveSyncSettingsEntity> LoadSettingsAsync(IServiceScope scope, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        return await db.SdActiveSyncSettings.FirstAsync(x => x.Id == 1, ct).ConfigureAwait(false);
    }

    protected override async Task SaveSettingsAsync(IServiceScope scope, SdActiveSyncSettingsEntity settings, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        db.Entry(settings).State = EntityState.Modified;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    protected override async Task<object> RunOnceAsync(IServiceScope scope, SyncTriggerKind trigger, CancellationToken ct)
    {
        var svc = scope.ServiceProvider.GetRequiredService<IVyjadreniHarvestService>();
        return await svc.HarvestScopeAsync(HarvestScope.Active, trigger, ct).ConfigureAwait(false);
    }
}
