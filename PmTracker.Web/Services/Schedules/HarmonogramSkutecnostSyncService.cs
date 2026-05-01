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
    /// <summary>
    /// DESIGN-4-A (2026-05-01) — pure compute, vrátí plán zamýšlených změn, NIC nezapisuje do DB.
    /// Použití: UI staging (PreviewSync endpoint), testy, manual review před commitem.
    /// </summary>
    Task<HarmonogramSyncPlan> ComputePlanAsync(int projektovyZaznamId, CancellationToken ct = default);

    /// <summary>
    /// DESIGN-4-A (2026-05-01) — aplikuje plán na DB s optimistic concurrency.
    /// Před zápisem ověří, že každý dotčený řádek má UpdatedAt odpovídající plánu.
    /// Pokud ne, skip rowu + log (stale token).
    /// </summary>
    Task<HarmonogramSyncResult> ApplyPlanAsync(HarmonogramSyncPlan plan, CancellationToken ct = default);

    /// <summary>
    /// Convenience wrapper: ComputePlanAsync + ApplyPlanAsync v sekvenci.
    /// Existující call sites (HarmonogramController, BindingRebalanceService, VyjadreniHarvestService)
    /// volají tuto metodu a fungují beze změny.
    /// </summary>
    Task<HarmonogramSyncResult> SyncZaznamAsync(int projektovyZaznamId, CancellationToken ct = default);

    /// <summary>
    /// Plán 4 Feature C Task 6 UI — načte aktivní bindings záznamu se doplněným TypZaznamu +
    /// PredikatKey, pro reuse v UI builderu (kandidáti pro dropdown výběr).
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

    /// <summary>
    /// DESIGN-4-A (2026-05-01) — pure read+compute, NIC nezapisuje do DB.
    /// Načte schéma + bindings + existing rows, spočítá zamýšlené změny per krok přes resolver,
    /// vrátí <see cref="HarmonogramSyncPlan"/> s detailem každé změny + ExpectedUpdatedAt token.
    /// </summary>
    public async Task<HarmonogramSyncPlan> ComputePlanAsync(int projektovyZaznamId, CancellationToken ct = default)
    {
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        if (projektovyZaznamId <= 0)
        {
            return new HarmonogramSyncPlan(projektovyZaznamId, nowUtc, Array.Empty<HarmonogramRowChange>());
        }

        var zaznam = await _db.ProjektoveZaznamy.AsNoTracking()
            .FirstOrDefaultAsync(z => z.Id == projektovyZaznamId, ct)
            .ConfigureAwait(false);
        if (zaznam is null)
        {
            return new HarmonogramSyncPlan(projektovyZaznamId, nowUtc, Array.Empty<HarmonogramRowChange>());
        }

        var schemaRaw = await _db.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(t => t.SablonaVerze == zaznam.HarmonogramSablonaVerze)
            .Select(t => new { t.Id, t.KrokPoradi, t.JeZpozdeni, t.Hodnota })
            .ToListAsync(ct).ConfigureAwait(false);
        var delayTypIdByPoradi = schemaRaw
            .Where(x => x.JeZpozdeni)
            .GroupBy(x => x.KrokPoradi)
            .ToDictionary(g => g.Key, g => g.First().Id);
        var durationTypIdByPoradi = schemaRaw
            .Where(x => !x.JeZpozdeni)
            .GroupBy(x => x.KrokPoradi)
            .ToDictionary(g => g.Key, g => g.First().Id);
        var defaultDurationByPoradi = schemaRaw
            .Where(x => !x.JeZpozdeni)
            .GroupBy(x => x.KrokPoradi)
            .ToDictionary(g => g.Key, g => g.First().Hodnota);

        if (delayTypIdByPoradi.Count == 0)
        {
            return new HarmonogramSyncPlan(projektovyZaznamId, nowUtc, Array.Empty<HarmonogramRowChange>());
        }

        var allRows = await _db.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(h => h.ZaznamId == projektovyZaznamId)
            .ToListAsync(ct).ConfigureAwait(false);
        var delayRowByTypId = allRows
            .Where(r => delayTypIdByPoradi.Values.Contains(r.TypId))
            .ToDictionary(r => r.TypId, r => r);
        // DESIGN-10-A: NULL DURATION → fallback default ze schématu, NULL HodnotaInt → 0.
        var durationByPoradi = durationTypIdByPoradi
            .ToDictionary(kv => kv.Key, kv =>
            {
                var row = allRows.FirstOrDefault(r => r.TypId == kv.Value);
                return row?.HodnotaInt ?? defaultDurationByPoradi.GetValueOrDefault(kv.Key);
            });

        // Pre-compute baseline end datum per krok (cumulative prefix sum durations).
        var baselineEndByPoradi = new Dictionary<int, DateTime>();
        var cumulative = zaznam.DatumZalozeni.Date;
        foreach (var poradi in delayTypIdByPoradi.Keys.OrderBy(p => p))
        {
            cumulative = cumulative.AddDays(durationByPoradi.GetValueOrDefault(poradi));
            baselineEndByPoradi[poradi] = cumulative;
        }

        var bindings = await LoadBindingKandidatiAsync(projektovyZaznamId, ct).ConfigureAwait(false);

        var changes = new List<HarmonogramRowChange>();
        foreach (var (poradi, delayTypId) in delayTypIdByPoradi)
        {
            if (!delayRowByTypId.TryGetValue(delayTypId, out var row))
            {
                // Delay row pro tento krok neexistuje. Sync metadata se nevytváří "do zásoby" —
                // resolver si sáhne až když hodnota dorazí (existing chování zachováno).
                continue;
            }

            if (row.SkutecnostRezim == SkutecnostRezimEnum.Manual)
            {
                changes.Add(new HarmonogramRowChange(
                    row.Id, row.TypId, poradi,
                    OldHodnotaInt: row.HodnotaInt,
                    NewHodnotaInt: row.HodnotaInt,
                    OldZdroj: row.SkutecnostZdroj,
                    NewZdroj: row.SkutecnostZdroj,
                    OldPreferredExterniOdkazId: row.PreferredExterniOdkazId,
                    NewPreferredExterniOdkazId: row.PreferredExterniOdkazId,
                    ExpectedUpdatedAt: row.UpdatedAt,
                    Reason: HarmonogramRowChangeReason.SkippedManualRezim));
                continue;
            }

            var resolved = HarmonogramSkutecnostResolver.Resolve(poradi, bindings, row.PreferredExterniOdkazId);

            if (resolved.Datum is null)
            {
                // Nenašel se žádný kandidát. Pokud byl řádek dříve Automat, retract → Neznamo + NULL.
                if (row.SkutecnostZdroj == SkutecnostZdrojEnum.Automat)
                {
                    changes.Add(new HarmonogramRowChange(
                        row.Id, row.TypId, poradi,
                        OldHodnotaInt: row.HodnotaInt,
                        NewHodnotaInt: null, // DESIGN-10-A: retract na NULL
                        OldZdroj: row.SkutecnostZdroj,
                        NewZdroj: SkutecnostZdrojEnum.Neznamo,
                        OldPreferredExterniOdkazId: row.PreferredExterniOdkazId,
                        NewPreferredExterniOdkazId: null,
                        ExpectedUpdatedAt: row.UpdatedAt,
                        Reason: HarmonogramRowChangeReason.RetractAutomat_NoCandidates));
                }
                continue;
            }

            // Auto-fill: spočítat delay z resolved.Datum + zapsat metadata.
            var newPreferred = resolved.PreferredFallbackApplied ? null : row.PreferredExterniOdkazId;
            int? computedDelay = baselineEndByPoradi.TryGetValue(poradi, out var baselineEnd)
                ? (int)Math.Round((resolved.Datum.Value.Date - baselineEnd.Date).TotalDays)
                : row.HodnotaInt;

            var hasChange = row.HodnotaInt != computedDelay
                || row.SkutecnostZdroj != SkutecnostZdrojEnum.Automat
                || row.PreferredExterniOdkazId != newPreferred;

            if (!hasChange) continue;

            var reason = resolved.PreferredFallbackApplied ? HarmonogramRowChangeReason.PreferredFallback
                : row.SkutecnostZdroj != SkutecnostZdrojEnum.Automat ? HarmonogramRowChangeReason.NewAutomatValue
                : HarmonogramRowChangeReason.UpdatedAutomatValue;

            changes.Add(new HarmonogramRowChange(
                row.Id, row.TypId, poradi,
                OldHodnotaInt: row.HodnotaInt,
                NewHodnotaInt: computedDelay,
                OldZdroj: row.SkutecnostZdroj,
                NewZdroj: SkutecnostZdrojEnum.Automat,
                OldPreferredExterniOdkazId: row.PreferredExterniOdkazId,
                NewPreferredExterniOdkazId: newPreferred,
                ExpectedUpdatedAt: row.UpdatedAt,
                Reason: reason));
        }

        return new HarmonogramSyncPlan(projektovyZaznamId, nowUtc, changes);
    }

    /// <summary>
    /// DESIGN-4-A (2026-05-01) — aplikace plánu na DB s optimistic concurrency token.
    /// Před zápisem ověří, že každý dotčený řádek má UpdatedAt odpovídající plánu.
    /// Pokud ne, skip rowu + log (stale token = race condition s jiným syncem).
    /// </summary>
    public async Task<HarmonogramSyncResult> ApplyPlanAsync(HarmonogramSyncPlan plan, CancellationToken ct = default)
    {
        if (plan.Changes.Count == 0)
        {
            return new HarmonogramSyncResult(plan.ProjektovyZaznamId, 0, 0, 0, 0);
        }

        var rowIds = plan.Changes.Select(c => c.RowId).Distinct().ToList();
        var trackedRows = await _db.ZaznamHarmonogramHodnoty
            .Where(h => rowIds.Contains(h.Id))
            .ToListAsync(ct).ConfigureAwait(false);
        var rowById = trackedRows.ToDictionary(r => r.Id);

        int updated = 0, skipManual = 0, noKandidat = 0, preferredFallbacks = 0, staleSkipped = 0;
        var nowUtc = _time.GetUtcNow().UtcDateTime;

        foreach (var change in plan.Changes)
        {
            if (!rowById.TryGetValue(change.RowId, out var row))
            {
                staleSkipped++; // row deleted between Compute and Apply
                continue;
            }

            // Optimistic concurrency: row UpdatedAt musí matchovat plan ExpectedUpdatedAt.
            if (row.UpdatedAt != change.ExpectedUpdatedAt)
            {
                staleSkipped++;
                _logger.LogWarning(
                    "ApplyPlan: row {RowId} stale token (expected {Exp:o}, actual {Act:o}) — skipped.",
                    change.RowId, change.ExpectedUpdatedAt, row.UpdatedAt);
                continue;
            }

            if (change.Reason == HarmonogramRowChangeReason.SkippedManualRezim)
            {
                skipManual++;
                continue;
            }

            if (change.Reason == HarmonogramRowChangeReason.PreferredFallback)
            {
                preferredFallbacks++;
            }

            row.HodnotaInt = change.NewHodnotaInt;
            row.SkutecnostZdroj = change.NewZdroj;
            row.PreferredExterniOdkazId = change.NewPreferredExterniOdkazId;
            row.UpdatedAt = nowUtc;
            updated++;
        }

        if (updated > 0)
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "ApplyPlan #{Id}: updated={U} skipManual={SM} noKandidat={NK} preferredFallback={PF} stale={ST}",
            plan.ProjektovyZaznamId, updated, skipManual, noKandidat, preferredFallbacks, staleSkipped);

        return new HarmonogramSyncResult(
            plan.ProjektovyZaznamId, updated, skipManual, noKandidat, preferredFallbacks);
    }

    /// <summary>
    /// Convenience wrapper: ComputePlanAsync + ApplyPlanAsync v sekvenci.
    /// Existující call sites (HarmonogramController, BindingRebalanceService, VyjadreniHarvestService)
    /// volají tuto metodu a fungují beze změny.
    /// </summary>
    public async Task<HarmonogramSyncResult> SyncZaznamAsync(int projektovyZaznamId, CancellationToken ct = default)
    {
        var plan = await ComputePlanAsync(projektovyZaznamId, ct).ConfigureAwait(false);
        return await ApplyPlanAsync(plan, ct).ConfigureAwait(false);
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
