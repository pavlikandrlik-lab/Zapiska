using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Plán 4 Feature C Task 4 — orchestrace auto-fill skutečnosti.
/// Kaskádově přepočítá <see cref="ZaznamHarmonogramHodnotaEntity.SkutecnostZdroj"/> +
/// <see cref="ZaznamHarmonogramHodnotaEntity.PreferredExterniOdkazId"/> pro HS0X_DELAY řádky
/// daného záznamu. Respektuje user override (<see cref="SkutecnostRezimEnum.Manual"/> skip).
/// </summary>
public interface IHarmonogramSkutecnostSyncService
{
    Task<HarmonogramSyncResult> SyncZaznamAsync(int projektovyZaznamId, CancellationToken ct = default);

    /// <summary>
    /// Plán 4 Feature C Task 6 UI — načte aktivní bindings záznamu se doplněným TypZaznamu +
    /// PredikatKey, pro reuse v UI builderu (kandidáti pro dropdown výběr).
    /// Interně používá stejný pattern jako <see cref="SyncZaznamAsync"/>.
    /// </summary>
    Task<IReadOnlyList<BindingKandidat>> GetKandidatiForZaznamAsync(int zaznamId, CancellationToken ct = default);
}

/// <summary>Souhrn výsledku sync operace pro jeden záznam.</summary>
public sealed record HarmonogramSyncResult(
    int ProjektovyZaznamId,
    int KrokuAktualizovano,
    int KrokuPreskoceno_Manual,
    int KrokuBezKandidatu,
    int PreferredFallbackPouzito);

/// <summary>
/// Impl. Načte schema + active bindings s fingerprint-cached TypZaznamu,
/// spustí <see cref="HarmonogramSkutecnostResolver"/> per krok a persistuje Zdroj/Preferred.
/// Samotný delay výpočet (HodnotaInt v dnech) zůstává v existujícím flow —
/// sync pouze audituje zdroj a užitečná data pro UI badge.
/// </summary>
public sealed class HarmonogramSkutecnostSyncService : IHarmonogramSkutecnostSyncService
{
    private readonly PmTrackerDbContext _db;
    private readonly IVyjadreniQueryService _vyjadreniQuery;
    private readonly TimeProvider _time;
    private readonly ILogger<HarmonogramSkutecnostSyncService> _logger;

    public HarmonogramSkutecnostSyncService(
        PmTrackerDbContext db,
        IVyjadreniQueryService vyjadreniQuery,
        TimeProvider time,
        ILogger<HarmonogramSkutecnostSyncService> logger)
    {
        _db = db;
        _vyjadreniQuery = vyjadreniQuery;
        _time = time;
        _logger = logger;
    }

    public async Task<HarmonogramSyncResult> SyncZaznamAsync(int projektovyZaznamId, CancellationToken ct = default)
    {
        if (projektovyZaznamId <= 0)
        {
            return new HarmonogramSyncResult(projektovyZaznamId, 0, 0, 0, 0);
        }

        var zaznam = await _db.ProjektoveZaznamy.AsNoTracking()
            .FirstOrDefaultAsync(z => z.Id == projektovyZaznamId, ct)
            .ConfigureAwait(false);
        if (zaznam is null)
        {
            return new HarmonogramSyncResult(projektovyZaznamId, 0, 0, 0, 0);
        }

        // 1) Schema: pro každý KrokPoradi najdi DelayTypId
        var schema = await _db.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(t => t.SablonaVerze == zaznam.HarmonogramSablonaVerze && t.JeZpozdeni)
            .Select(t => new { t.Id, t.KrokPoradi })
            .ToListAsync(ct).ConfigureAwait(false);
        var delayTypIdByPoradi = schema
            .GroupBy(x => x.KrokPoradi)
            .ToDictionary(g => g.Key, g => g.First().Id);

        if (delayTypIdByPoradi.Count == 0)
        {
            return new HarmonogramSyncResult(projektovyZaznamId, 0, 0, 0, 0);
        }

        // 2) Load / create HS0X_DELAY row per krok (tracked, ne AsNoTracking — píšeme)
        var delayRows = await _db.ZaznamHarmonogramHodnoty
            .Where(h => h.ZaznamId == projektovyZaznamId
                     && delayTypIdByPoradi.Values.Contains(h.TypId))
            .ToListAsync(ct).ConfigureAwait(false);
        var delayRowByTypId = delayRows.ToDictionary(r => r.TypId, r => r);

        // 3) Načtení bindings + fingerprint pro TypZaznamu
        var bindings = await LoadBindingKandidatiAsync(projektovyZaznamId, ct).ConfigureAwait(false);

        // 4) Per krok: resolve + update metadata
        int updated = 0, skipManual = 0, noKandidat = 0, preferredFallbacks = 0;
        var nowUtc = _time.GetUtcNow().UtcDateTime;

        foreach (var (poradi, delayTypId) in delayTypIdByPoradi)
        {
            if (!delayRowByTypId.TryGetValue(delayTypId, out var row))
            {
                // Delay row pro tento krok neexistuje (ještě nikdo nevyplnil skutečnost).
                // Sync metadata se nevytváří „do zásoby" — resolver si sáhne až když hodnota dorazí.
                if (HarmonogramSkutecnostResolver.Resolve(poradi, bindings, preferredExterniOdkazId: null)
                        .Datum is null)
                {
                    noKandidat++;
                    continue;
                }
                // Kandidát existuje, ale delay row zatím neleží — caller (harvester / UI save flow)
                // založí row s aktuální hodnotou a příští sync už bude mít co aktualizovat.
                noKandidat++;
                continue;
            }

            if (row.SkutecnostRezim == SkutecnostRezimEnum.Manual)
            {
                skipManual++;
                continue;
            }

            var resolved = HarmonogramSkutecnostResolver.Resolve(
                poradi, bindings, row.PreferredExterniOdkazId);

            if (resolved.Datum is null)
            {
                // Nenašel se žádný kandidát. Pokud byl řádek dříve Automat, „retract" → Neznamo.
                if (row.SkutecnostZdroj == SkutecnostZdrojEnum.Automat)
                {
                    row.SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo;
                    row.PreferredExterniOdkazId = null;
                    row.UpdatedAt = nowUtc;
                    updated++;
                }
                else
                {
                    noKandidat++;
                }
                continue;
            }

            if (resolved.PreferredFallbackApplied)
            {
                preferredFallbacks++;
            }

            // Metadata update: Zdroj → Automat (auto-filled), Preferred podle výsledku resolveru.
            // Hodnotu HodnotaInt (delay ve dnech) aktuální sync _nemění_ — delay se počítá
            // existujícím flow (BindingRebalanceService / ScheduleActualSourceResolver) a této
            // službě zatím stačí audit Zdroje a volba kandidáta.
            var changed = false;
            if (row.SkutecnostZdroj != SkutecnostZdrojEnum.Automat)
            {
                row.SkutecnostZdroj = SkutecnostZdrojEnum.Automat;
                changed = true;
            }
            var newPreferred = resolved.PreferredFallbackApplied
                ? null
                : row.PreferredExterniOdkazId; // zachováme user volbu pokud zůstala platná
            if (row.PreferredExterniOdkazId != newPreferred)
            {
                row.PreferredExterniOdkazId = newPreferred;
                changed = true;
            }
            if (changed)
            {
                row.UpdatedAt = nowUtc;
                updated++;
            }
        }

        if (updated > 0)
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Harmonogram sync záznam #{Id}: updated={U} skipManual={SM} noKandidat={NK} preferredFallbacks={PF}",
            projektovyZaznamId, updated, skipManual, noKandidat, preferredFallbacks);

        return new HarmonogramSyncResult(projektovyZaznamId, updated, skipManual, noKandidat, preferredFallbacks);
    }

    /// <summary>
    /// Plán 4 Feature C Task 6 UI — veřejný entry point pro builder, deleguje na privátní helper.
    /// </summary>
    public Task<IReadOnlyList<BindingKandidat>> GetKandidatiForZaznamAsync(int zaznamId, CancellationToken ct = default)
        => LoadBindingKandidatiAsync(zaznamId, ct);

    /// <summary>
    /// Načte aktivní bindings pro záznam a doplní <c>TypZaznamu</c> přes fingerprint lookup
    /// (TypZaznamu není lokálně cached na externi_odkaz — viz current schema).
    /// Pro každé vyjádření určí <c>PredikatKey</c> podle (TypZaznamu × KrokPoradi) přes matici.
    /// </summary>
    private async Task<IReadOnlyList<BindingKandidat>> LoadBindingKandidatiAsync(
        int zaznamId, CancellationToken ct)
    {
        // Join bindings × externi_odkaz × schema (KrokKey → KrokPoradi)
        var raw = await (
            from v in _db.VyjadreniVazby.AsNoTracking()
            where v.ZaznamId == zaznamId && v.Stav == (byte)VazbaStav.Active
            join eo in _db.ZaznamExterniOdkazy.AsNoTracking() on v.ExterniOdkazId equals eo.Id
            where eo.Cislo != null
            join t in _db.CiselnikHarmonogramTypu.AsNoTracking() on v.KrokKey equals t.KrokKey
            where !t.JeZpozdeni
            select new
            {
                v.ExterniOdkazId,
                eo.Cislo,
                v.KrokKey,
                t.KrokPoradi,
                v.DatumVyjadreni,
            })
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        if (raw.Count == 0)
        {
            return Array.Empty<BindingKandidat>();
        }

        // Fingerprint lookup pro TypZaznamu — batch over distinct cisla
        var cisla = raw.Select(r => r.Cislo!).Distinct().ToList();
        IReadOnlyDictionary<string, HotZaznamFingerprintDto> fp;
        try
        {
            fp = await _vyjadreniQuery.GetHotZaznamFingerprintsAsync(cisla, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "HarmonogramSkutecnostSyncService: fingerprint lookup selhal pro záznam {ZaznamId}, TypZaznamu nedostupný.",
                zaznamId);
            fp = new Dictionary<string, HotZaznamFingerprintDto>();
        }

        var result = new List<BindingKandidat>(raw.Count);
        foreach (var r in raw)
        {
            if (!fp.TryGetValue(r.Cislo!, out var primary) || string.IsNullOrWhiteSpace(primary.TypZaznamu))
            {
                continue;
            }
            var predikatKey = HarmonogramKrokDatumMapping.GetPredikatKey(primary.TypZaznamu, r.KrokPoradi);
            if (predikatKey is null)
            {
                continue;
            }
            result.Add(new BindingKandidat(
                ExterniOdkazId: r.ExterniOdkazId,
                Cislo6: r.Cislo!,
                TypZaznamu: primary.TypZaznamu,
                PredikatKey: predikatKey,
                Datum: r.DatumVyjadreni));
        }
        return result;
    }
}
