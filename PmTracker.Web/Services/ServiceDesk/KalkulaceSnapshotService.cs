using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.ServiceDesk;

public interface IKalkulaceSnapshotService
{
    /// <summary>Obnoví snímek skutečné ceny u daných vazeb. Jiné vazby než PNF přeskočí.</summary>
    Task SyncAsync(IReadOnlyCollection<int> externiOdkazIds, CancellationToken ct);
}

/// <summary>
/// Snímek skutečné ceny PNF z akceptované kalkulace (spec 2026-09-10 část A).
///
/// Běží vedle harvestu vyjádření, ne v něm: harvest má rychlou cestu podle fingerprintu
/// tiketu, jenže akceptace kalkulace mění HOT_KALKULACE, ne fingerprint. Za rychlou cestou
/// by se snímek po akceptaci nikdy neobnovil.
///
/// Pravidla (R6): tiket nenalezen → beze změny (vypnutý ServiceDesk nebo smazaný tiket);
/// tiket nalezen bez akceptované kalkulace → vynulovat (mohla být odvolána); jinak zapsat.
/// Kalkulace se vybírá stejně jako pro tisk výzvy — GetAkceptovaneKalkulaceAsync.
/// Čas se zapisuje jen při změně, jinak by periodický běh přepisoval řádky všech PNF.
/// </summary>
public sealed class KalkulaceSnapshotService : IKalkulaceSnapshotService
{
    private const string PnfKod = "PNF";

    private readonly PmTrackerDbContext _db;
    private readonly ITicketingQueryService _ticketing;
    private readonly TimeProvider _time;
    private readonly ILogger<KalkulaceSnapshotService> _logger;

    public KalkulaceSnapshotService(
        PmTrackerDbContext db,
        ITicketingQueryService ticketing,
        TimeProvider time,
        ILogger<KalkulaceSnapshotService> logger)
    {
        _db = db;
        _ticketing = ticketing;
        _time = time;
        _logger = logger;
    }

    public async Task SyncAsync(IReadOnlyCollection<int> externiOdkazIds, CancellationToken ct)
    {
        if (externiOdkazIds.Count == 0) return;

        var pnfTypId = await _db.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Where(t => t.Kod == PnfKod)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (pnfTypId is null) return;

        var ids = externiOdkazIds.Distinct().ToArray();
        var vazby = await _db.ZaznamExterniOdkazy
            .Where(x => ids.Contains(x.Id) && x.TypOdkazuId == pnfTypId.Value && x.Cislo != "")
            .ToListAsync(ct).ConfigureAwait(false);
        if (vazby.Count == 0) return;

        var hot = await _ticketing
            .GetZaznamyAsync(vazby.Select(v => v.Cislo).Distinct().ToArray(), ct)
            .ConfigureAwait(false);
        var pidy = hot.Values
            .Select(h => h.Pid)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct()
            .ToArray();
        var kalkulace = await _ticketing.GetAkceptovaneKalkulaceAsync(pidy, ct).ConfigureAwait(false);

        var ted = _time.GetUtcNow().UtcDateTime;
        var zmeneno = 0;
        foreach (var vazba in vazby)
        {
            if (!hot.TryGetValue(vazba.Cislo, out var tiket)) continue;

            var k = string.IsNullOrWhiteSpace(tiket.Pid) ? null : kalkulace.GetValueOrDefault(tiket.Pid!);
            var cena = k?.CenaCelkem;
            long? kalkulaceId = k?.Id;
            if (vazba.KalkulaceCena == cena && vazba.KalkulaceId == kalkulaceId) continue;

            vazba.KalkulaceCena = cena;
            vazba.KalkulaceId = kalkulaceId;
            vazba.KalkulaceNacteno = ted;
            zmeneno++;
        }

        if (zmeneno > 0)
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("KalkulaceSnapshot: změněn snímek u {Pocet} PNF vazeb.", zmeneno);
        }
    }
}
