using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Periodic hosted service pro scope = archivní tickety (HOT_ZAZNAMY.stav = 'archiv').
/// Default perioda 1440 min (24 h). Paralelní s <see cref="SdActivePeriodicSyncHostedService"/>,
/// ale běží méně často — off-peak. Spec §4.
/// </summary>
public sealed class SdArchivePeriodicSyncHostedService : SyncHostedServiceBase<SdArchiveSyncSettingsEntity>
{
    public SdArchivePeriodicSyncHostedService(
        IServiceScopeFactory scopeFactory,
        ISyncJobRunLock<SdArchiveSyncSettingsEntity> runLock,
        ManualTriggerSignal<SdArchiveSyncSettingsEntity> manualSignal,
        TimeProvider timeProvider,
        ILogger<SdArchivePeriodicSyncHostedService> logger)
        : base(scopeFactory, runLock, manualSignal, timeProvider, logger) { }

    protected override string JobKey => "sd.archive";

    protected override async Task<SdArchiveSyncSettingsEntity> LoadSettingsAsync(IServiceScope scope, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        return await db.SdArchiveSyncSettings.FirstAsync(x => x.Id == 1, ct).ConfigureAwait(false);
    }

    protected override async Task SaveSettingsAsync(IServiceScope scope, SdArchiveSyncSettingsEntity settings, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        db.Entry(settings).State = EntityState.Modified;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    protected override async Task<object> RunOnceAsync(IServiceScope scope, SyncTriggerKind trigger, CancellationToken ct)
    {
        var svc = scope.ServiceProvider.GetRequiredService<IVyjadreniHarvestService>();
        return await svc.HarvestScopeAsync(HarvestScope.Archive, trigger, ct).ConfigureAwait(false);
    }
}
