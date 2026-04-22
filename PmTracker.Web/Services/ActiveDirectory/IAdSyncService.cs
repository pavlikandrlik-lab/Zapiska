using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public interface IAdSyncService
{
    Task<AdSyncResult> SyncAllPeopleAsync(SyncTriggerKind trigger, CancellationToken ct = default);

    Task<AdSinglePersonSyncResult> SyncSinglePersonAsync(int osobaId, CancellationToken ct = default);
}
