using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Admin handler pro SD archive periodic job. JobKey = "sd.archive".
/// </summary>
public sealed class SdArchiveSyncJobAdminHandler : SyncJobAdminHandlerBase<SdArchiveSyncSettingsEntity>
{
    public SdArchiveSyncJobAdminHandler(
        PmTrackerDbContext db,
        ISyncJobRunLock<SdArchiveSyncSettingsEntity> runLock,
        ManualTriggerSignal<SdArchiveSyncSettingsEntity> manualSignal,
        TimeProvider time)
        : base(db, runLock, manualSignal, time)
    {
    }

    public override string JobKey => "sd.archive";
    protected override string Title => "ServiceDesk — archivní tickety";
    protected override string Description =>
        "Pravidelné načítání vyjádření z HOT_VYJADRENI pro archivované tickety (stav = 'archiv'). Běží méně často.";

    protected override IQueryable<SdArchiveSyncSettingsEntity> Query()
        => Db.SdArchiveSyncSettings.Where(x => x.Id == 1);

    protected override Task<SdArchiveSyncSettingsEntity> LoadForUpdateAsync(CancellationToken ct)
        => Db.SdArchiveSyncSettings.FirstAsync(x => x.Id == 1, ct);

    // Viz komentář v SdActiveSyncJobAdminHandler — SD formát používá "ticketsDrilled".
    protected override IReadOnlyList<string> OkCountFields { get; } = new[] { "ticketsDrilled", "okCount" };
}
