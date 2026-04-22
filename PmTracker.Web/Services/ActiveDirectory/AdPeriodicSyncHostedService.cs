using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class AdPeriodicSyncHostedService : SyncHostedServiceBase<AdSyncSettingsEntity>
{
    public AdPeriodicSyncHostedService(
        IServiceScopeFactory scopeFactory,
        ISyncJobRunLock<AdSyncSettingsEntity> runLock,
        ManualTriggerSignal<AdSyncSettingsEntity> manualSignal,
        TimeProvider timeProvider,
        ILogger<AdPeriodicSyncHostedService> logger)
        : base(scopeFactory, runLock, manualSignal, timeProvider, logger) { }

    protected override string JobKey => "ad.periodic";

    protected override async Task<AdSyncSettingsEntity> LoadSettingsAsync(IServiceScope scope, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        return await db.AdSyncSettings.FirstAsync(x => x.Id == 1, ct).ConfigureAwait(false);
    }

    protected override async Task SaveSettingsAsync(IServiceScope scope, AdSyncSettingsEntity settings, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        db.Entry(settings).State = EntityState.Modified;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    protected override async Task<object> RunOnceAsync(IServiceScope scope, SyncTriggerKind trigger, CancellationToken ct)
    {
        var svc = scope.ServiceProvider.GetRequiredService<IAdSyncService>();
        return await svc.SyncAllPeopleAsync(trigger, ct).ConfigureAwait(false);
    }
}
