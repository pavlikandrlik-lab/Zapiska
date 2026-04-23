using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class AdSyncJobAdminHandler : SyncJobAdminHandlerBase<AdSyncSettingsEntity>
{
    public AdSyncJobAdminHandler(
        PmTrackerDbContext db,
        ISyncJobRunLock<AdSyncSettingsEntity> runLock,
        ManualTriggerSignal<AdSyncSettingsEntity> manualSignal,
        TimeProvider time)
        : base(db, runLock, manualSignal, time)
    {
    }

    public override string JobKey => "ad.periodic";
    protected override string Title => "Synchronizace s AD";
    protected override string Description => "Pravidelná aktualizace osob s vyplněným GuidAd.";

    protected override IQueryable<AdSyncSettingsEntity> Query() => Db.AdSyncSettings.Where(x => x.Id == 1);

    protected override Task<AdSyncSettingsEntity> LoadForUpdateAsync(CancellationToken ct)
        => Db.AdSyncSettings.FirstAsync(x => x.Id == 1, ct);
}
