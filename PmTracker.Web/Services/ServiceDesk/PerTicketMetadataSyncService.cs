using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.ServiceDesk;

public interface IPerTicketMetadataSyncService
{
    /// <summary>
    /// Pro daný externí odkaz vyextrahuje 4 datumy a zapíše je do
    /// <c>ZaznamExterniOdkazEntity</c>.<c>DatumObjednani / PlanDodani / DatumDodani / DatumPrevzeti</c>.
    /// Auto-fill je default — přepisuje předchozí hodnoty (žádný respekt k ručnímu zápisu, viz spec §4 Q5).
    /// </summary>
    Task SyncTicketAsync(int externiOdkazId, CancellationToken ct);
}

/// <summary>
/// Plán 2026-04-28 Task 4 — orchestrace zápisu 4 datumů na <c>zaznam_externi_odkazy</c>.
/// Logika extrakce je v <see cref="PerTicketMetadataExtractor"/>; tato třída čte vstupní data
/// (<c>HotZaznamFingerprintDto.SlaDeadline</c>, vyjádření tiketu) a po extrakci propíše rozdíly
/// do entity přes EF change tracking.
///
/// Volá se jako best-effort z <c>VyjadreniHarvestService</c> po commitu harvest batch.
/// </summary>
public sealed class PerTicketMetadataSyncService : IPerTicketMetadataSyncService
{
    private readonly PmTrackerDbContext _db;
    private readonly IVyjadreniQueryService _vyjadreni;
    private readonly ILogger<PerTicketMetadataSyncService> _logger;

    public PerTicketMetadataSyncService(
        PmTrackerDbContext db,
        IVyjadreniQueryService vyjadreni,
        ILogger<PerTicketMetadataSyncService> logger)
    {
        _db = db;
        _vyjadreni = vyjadreni;
        _logger = logger;
    }

    public async Task SyncTicketAsync(int externiOdkazId, CancellationToken ct)
    {
        var eo = await _db.ZaznamExterniOdkazy
            .FirstOrDefaultAsync(x => x.Id == externiOdkazId, ct).ConfigureAwait(false);

        if (eo is null || string.IsNullOrWhiteSpace(eo.Cislo))
        {
            return;
        }

        // Primary fingerprint dává typ tiketu + sla_deadline (pro NES PlanDodani).
        var fingerprints = await _vyjadreni
            .GetHotZaznamFingerprintsAsync(new[] { eo.Cislo! }, ct)
            .ConfigureAwait(false);

        if (!fingerprints.TryGetValue(eo.Cislo!, out var fp))
        {
            // Tiket neexistuje v HOT_ZAZNAMY — nelze určit typ ani sla_deadline.
            return;
        }

        // Načti všechna vyjádření tiketu chronologicky (od počátku, ne inkrementálně —
        // potřebujeme úplný kontext pro správné určení DatumObjednani/DatumPrevzeti).
        var vyjadreni = await _vyjadreni
            .GetVyjadreniForTicketAsync(eo.Cislo!, sinceUtc: null, ct)
            .ConfigureAwait(false);

        var meta = PerTicketMetadataExtractor.Extract(fp.TypZaznamu, fp.SlaDeadline, vyjadreni);

        bool changed = false;
        if (eo.DatumObjednani != meta.DatumObjednani)
        {
            eo.DatumObjednani = meta.DatumObjednani;
            changed = true;
        }
        if (eo.PlanDodani != meta.PlanDodani)
        {
            eo.PlanDodani = meta.PlanDodani;
            changed = true;
        }
        if (eo.DatumDodani != meta.DatumDodani)
        {
            eo.DatumDodani = meta.DatumDodani;
            changed = true;
        }
        if (eo.DatumPrevzeti != meta.DatumPrevzeti)
        {
            eo.DatumPrevzeti = meta.DatumPrevzeti;
            changed = true;
        }

        if (changed)
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation(
                "PerTicketMetadataSync: externí odkaz #{Id} (cislo={Cislo}, typ={Typ}) — DatumObjednani={DO}, PlanDodani={PD}, DatumDodani={DD}, DatumPrevzeti={DP}",
                externiOdkazId, eo.Cislo, fp.TypZaznamu,
                meta.DatumObjednani, meta.PlanDodani, meta.DatumDodani, meta.DatumPrevzeti);
        }
    }
}
