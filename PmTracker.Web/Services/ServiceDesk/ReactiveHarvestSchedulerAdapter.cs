using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Implementace <see cref="IHarvestScheduler"/>, která jen zapisuje request do
/// sdílené reactive queue. Skutečný harvest provede <c>SdReactiveSyncConsumer</c>.
/// Nahrazuje <c>NoOpHarvestScheduler</c> ze stubu Plánu B.
///
/// Anti-spam: queue má producer-side dedup podle <see cref="SdReactiveHarvestRequest.DedupKey"/>
/// (= ExterniOdkazId), takže opakovaný enqueue stejného odkazu v krátkém okně je no-op
/// dokud consumer předchozí nezpracuje. Viz sync-infra spec §13 amendment + spec §13.4.
///
/// Per-ticket lock + fingerprint strategie ve <see cref="VyjadreniHarvestService"/>
/// (Task 6) zaručují, že i kdyby dedup propustil duplicitu, drill proběhne nejvýš
/// jednou per ticket — fingerprint skip pak data idempotentně neaktualizuje.
/// </summary>
public sealed class ReactiveHarvestSchedulerAdapter : IHarvestScheduler
{
    private readonly IReactiveSyncQueue<SdReactiveHarvestRequest> _queue;
    private readonly PmTrackerDbContext _db;

    public ReactiveHarvestSchedulerAdapter(
        IReactiveSyncQueue<SdReactiveHarvestRequest> queue,
        PmTrackerDbContext db)
    {
        _queue = queue;
        _db = db;
    }

    /// <summary>
    /// T2/T7 — harvest jedné externí vazby. Enqueue přímo podle ExterniOdkazId.
    /// </summary>
    public async Task ScheduleHarvestAsync(int externiOdkazId, CancellationToken ct = default, SdReactiveSource source = SdReactiveSource.RecordSave)
    {
        // Sanity check — pokud záznam neexistuje, nemá smysl enqueuovat.
        var exists = await _db.ZaznamExterniOdkazy
            .AsNoTracking()
            .AnyAsync(x => x.Id == externiOdkazId, ct)
            .ConfigureAwait(false);

        if (!exists)
        {
            return;
        }

        await _queue.EnqueueAsync(
            new SdReactiveHarvestRequest(externiOdkazId, source),
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// T5/T8 — harvest všech externích vazeb daného projektového záznamu.
    /// Enumerace vytáhne všechny `zaznam_externi_odkazy` s vyplněným `cislo` a enqueue jeden
    /// request per záznam. DedupKey = ExterniOdkazId, takže každý odkaz má nezávislou slot-pozici
    /// v queue (stejný záznam s více odkazy vytvoří více requestů — korrektní).
    /// </summary>
    public async Task ScheduleHarvestForRecordAsync(int zaznamId, CancellationToken ct = default, SdReactiveSource source = SdReactiveSource.EditorOpen)
    {
        var externiOdkazIds = await _db.ZaznamExterniOdkazy
            .AsNoTracking()
            .Where(x => x.ZaznamId == zaznamId && x.Cislo != null && x.Cislo != "")
            .Select(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var id in externiOdkazIds)
        {
            await _queue.EnqueueAsync(
                new SdReactiveHarvestRequest(id, source),
                ct).ConfigureAwait(false);
        }
    }
}
