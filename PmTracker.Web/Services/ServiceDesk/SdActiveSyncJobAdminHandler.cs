using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Admin handler pro SD active periodic job. JobKey = "sd.active".
/// </summary>
public sealed class SdActiveSyncJobAdminHandler : SyncJobAdminHandlerBase<SdActiveSyncSettingsEntity>
{
    public SdActiveSyncJobAdminHandler(
        PmTrackerDbContext db,
        ISyncJobRunLock<SdActiveSyncSettingsEntity> runLock,
        ManualTriggerSignal<SdActiveSyncSettingsEntity> manualSignal,
        TimeProvider time)
        : base(db, runLock, manualSignal, time)
    {
    }

    public override string JobKey => "sd.active";
    protected override string Title => "ServiceDesk — aktivní tickety";
    protected override string Description =>
        "Pravidelné načítání vyjádření z HOT_VYJADRENI pro aktivní tickety (stav <> 'archiv').";

    protected override IQueryable<SdActiveSyncSettingsEntity> Query()
        => Db.SdActiveSyncSettings.Where(x => x.Id == 1);

    protected override Task<SdActiveSyncSettingsEntity> LoadForUpdateAsync(CancellationToken ct)
        => Db.SdActiveSyncSettings.FirstAsync(x => x.Id == 1, ct);

    // SD emituje do LastResultJson pole "ticketsDrilled" (počet drilled ticketů v
    // tomto runu), které chceme prezentovat jako OkCount; teprve při absenci
    // fallback na generický "okCount".
    protected override IReadOnlyList<string> OkCountFields { get; } = new[] { "ticketsDrilled", "okCount" };
}
