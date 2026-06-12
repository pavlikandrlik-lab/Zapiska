using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Datum-model (2026-06-12) — orchestrace auto-fill skutečnosti. Per krok (1–10) resolvuje
/// kandidátní datum z vazeb (<see cref="HarmonogramSkutecnostResolver"/>) a zapisuje
/// <see cref="ZaznamHarmonogramKrokEntity.SkutecnostDatum"/> (+ Zdroj/Preferred). Manuální kroky
/// {2,5,8,9} se nevytěžují. Respektuje user override (<see cref="SkutecnostRezimEnum.Manual"/>).
/// </summary>
public interface IHarmonogramSkutecnostSyncService
{
    Task<HarmonogramSyncPlan> ComputePlanAsync(int projektovyZaznamId, CancellationToken ct = default);
    Task<HarmonogramSyncResult> ApplyPlanAsync(HarmonogramSyncPlan plan, CancellationToken ct = default);
    Task<HarmonogramSyncResult> SyncZaznamAsync(int projektovyZaznamId, CancellationToken ct = default);
    Task<IReadOnlyList<BindingKandidat>> GetKandidatiForZaznamAsync(int zaznamId, CancellationToken ct = default);
}

/// <summary>Souhrn výsledku sync operace pro jeden záznam.</summary>
public sealed record HarmonogramSyncResult(
    int ProjektovyZaznamId,
    int KrokuAktualizovano,
    int KrokuPreskoceno_Manual,
    int KrokuBezKandidatu,
    int PreferredFallbackPouzito,
    int StaleSkipped = 0);

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

    public async Task<HarmonogramSyncPlan> ComputePlanAsync(int projektovyZaznamId, CancellationToken ct = default)
    {
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        if (projektovyZaznamId <= 0)
        {
            return new HarmonogramSyncPlan(projektovyZaznamId, nowUtc, Array.Empty<HarmonogramKrokChange>());
        }

        var krokRows = await _db.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(k => k.ZaznamId == projektovyZaznamId)
            .ToListAsync(ct).ConfigureAwait(false);
        var rowByPoradi = krokRows.GroupBy(r => (int)r.Poradi).ToDictionary(g => g.Key, g => g.First());

        var bindings = await LoadBindingKandidatiAsync(projektovyZaznamId, ct).ConfigureAwait(false);

        var changes = new List<HarmonogramKrokChange>();
        foreach (var def in HarmonogramKroky.Vse)
        {
            if (def.JeManualni)
            {
                continue; // manuální kroky se nevytěžují
            }
            var poradi = def.Poradi;
            rowByPoradi.TryGetValue(poradi, out var row);

            if (row is not null && (SkutecnostRezimEnum)row.SkutecnostRezim == SkutecnostRezimEnum.Manual)
            {
                changes.Add(new HarmonogramKrokChange(
                    poradi, row.SkutecnostDatum, row.SkutecnostDatum,
                    (SkutecnostZdrojEnum)row.SkutecnostZdroj, (SkutecnostZdrojEnum)row.SkutecnostZdroj,
                    row.PreferredExterniOdkazId, row.PreferredExterniOdkazId,
                    row.UpdatedAt, HarmonogramRowChangeReason.SkippedManualRezim));
                continue;
            }

            var resolved = HarmonogramSkutecnostResolver.Resolve(poradi, bindings, row?.PreferredExterniOdkazId);

            if (resolved.Datum is null)
            {
                if (row is not null && (SkutecnostZdrojEnum)row.SkutecnostZdroj == SkutecnostZdrojEnum.Automat)
                {
                    changes.Add(new HarmonogramKrokChange(
                        poradi, row.SkutecnostDatum, null,
                        SkutecnostZdrojEnum.Automat, SkutecnostZdrojEnum.Neznamo,
                        row.PreferredExterniOdkazId, null,
                        row.UpdatedAt, HarmonogramRowChangeReason.RetractAutomat_NoCandidates));
                }
                continue;
            }

            var newDatum = resolved.Datum.Value.Date;
            var newPreferred = resolved.PreferredFallbackApplied ? null : row?.PreferredExterniOdkazId;

            if (row is null)
            {
                changes.Add(new HarmonogramKrokChange(
                    poradi, null, newDatum,
                    SkutecnostZdrojEnum.Neznamo, SkutecnostZdrojEnum.Automat,
                    null, newPreferred,
                    DateTime.MinValue, HarmonogramRowChangeReason.CreateAutomatRow));
                continue;
            }

            var oldZdroj = (SkutecnostZdrojEnum)row.SkutecnostZdroj;
            var hasChange = row.SkutecnostDatum != newDatum
                || oldZdroj != SkutecnostZdrojEnum.Automat
                || row.PreferredExterniOdkazId != newPreferred;
            if (!hasChange) continue;

            var reason = resolved.PreferredFallbackApplied ? HarmonogramRowChangeReason.PreferredFallback
                : oldZdroj != SkutecnostZdrojEnum.Automat ? HarmonogramRowChangeReason.NewAutomatValue
                : HarmonogramRowChangeReason.UpdatedAutomatValue;

            changes.Add(new HarmonogramKrokChange(
                poradi, row.SkutecnostDatum, newDatum,
                oldZdroj, SkutecnostZdrojEnum.Automat,
                row.PreferredExterniOdkazId, newPreferred,
                row.UpdatedAt, reason));
        }

        return new HarmonogramSyncPlan(projektovyZaznamId, nowUtc, changes);
    }

    public async Task<HarmonogramSyncResult> ApplyPlanAsync(HarmonogramSyncPlan plan, CancellationToken ct = default)
    {
        if (plan.Changes.Count == 0)
        {
            return new HarmonogramSyncResult(plan.ProjektovyZaznamId, 0, 0, 0, 0);
        }

        var trackedRows = await _db.ZaznamHarmonogramKroky
            .Where(k => k.ZaznamId == plan.ProjektovyZaznamId)
            .ToListAsync(ct).ConfigureAwait(false);
        var rowByPoradi = trackedRows.GroupBy(r => (int)r.Poradi).ToDictionary(g => g.Key, g => g.First());

        int updated = 0, skipManual = 0, preferredFallbacks = 0, staleSkipped = 0;
        var nowUtc = _time.GetUtcNow().UtcDateTime;

        foreach (var change in plan.Changes)
        {
            if (change.Reason == HarmonogramRowChangeReason.SkippedManualRezim)
            {
                skipManual++;
                continue;
            }

            if (change.Reason == HarmonogramRowChangeReason.PreferredFallback)
            {
                preferredFallbacks++;
            }

            if (!rowByPoradi.TryGetValue(change.Poradi, out var row))
            {
                // CreateAutomatRow: řádek pro krok ještě neexistuje → insert.
                var newRow = new ZaznamHarmonogramKrokEntity
                {
                    ZaznamId = plan.ProjektovyZaznamId,
                    Poradi = (byte)change.Poradi,
                    SkutecnostDatum = change.NewSkutecnostDatum,
                    SkutecnostRezim = (byte)SkutecnostRezimEnum.Auto,
                    SkutecnostZdroj = (byte)change.NewZdroj,
                    PreferredExterniOdkazId = change.NewPreferredExterniOdkazId,
                    UpdatedAt = nowUtc
                };
                _db.ZaznamHarmonogramKroky.Add(newRow);
                try
                {
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                    rowByPoradi[change.Poradi] = newRow;
                    updated++;
                }
                catch (DbUpdateException)
                {
                    _db.Entry(newRow).State = EntityState.Detached;
                    var winner = await _db.ZaznamHarmonogramKroky
                        .FirstOrDefaultAsync(k => k.ZaznamId == plan.ProjektovyZaznamId && k.Poradi == (byte)change.Poradi, ct)
                        .ConfigureAwait(false);
                    if (winner is not null)
                    {
                        winner.SkutecnostDatum = change.NewSkutecnostDatum;
                        winner.SkutecnostZdroj = (byte)change.NewZdroj;
                        winner.PreferredExterniOdkazId = change.NewPreferredExterniOdkazId;
                        winner.UpdatedAt = nowUtc;
                        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                        rowByPoradi[change.Poradi] = winner;
                        updated++;
                    }
                }
                continue;
            }

            // Optimistic concurrency token.
            if (row.UpdatedAt != change.ExpectedUpdatedAt)
            {
                staleSkipped++;
                _logger.LogWarning(
                    "ApplyPlan: krok {Poradi} stale token (expected {Exp:o}, actual {Act:o}) — skipped.",
                    change.Poradi, change.ExpectedUpdatedAt, row.UpdatedAt);
                continue;
            }

            row.SkutecnostDatum = change.NewSkutecnostDatum;
            row.SkutecnostZdroj = (byte)change.NewZdroj;
            row.PreferredExterniOdkazId = change.NewPreferredExterniOdkazId;
            row.UpdatedAt = nowUtc;
            updated++;
        }

        if (_db.ChangeTracker.HasChanges())
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "ApplyPlan #{Id}: updated={U} skipManual={SM} preferredFallback={PF} stale={ST}",
            plan.ProjektovyZaznamId, updated, skipManual, preferredFallbacks, staleSkipped);

        return new HarmonogramSyncResult(
            plan.ProjektovyZaznamId, updated, skipManual, KrokuBezKandidatu: 0, preferredFallbacks, staleSkipped);
    }

    public async Task<HarmonogramSyncResult> SyncZaznamAsync(int projektovyZaznamId, CancellationToken ct = default)
    {
        var plan = await ComputePlanAsync(projektovyZaznamId, ct).ConfigureAwait(false);
        return await ApplyPlanAsync(plan, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<BindingKandidat>> GetKandidatiForZaznamAsync(int zaznamId, CancellationToken ct = default)
        => LoadBindingKandidatiAsync(zaznamId, ct);

    /// <summary>
    /// Načte aktivní vazby záznamu (kandidáty), doplní TypZaznamu přes fingerprint lookup a
    /// PredikatKey podle (TypZaznamu × Poradi). Vazba nese <see cref="ZaznamHarmonogramVyjadreniVazbaEntity.Poradi"/>.
    /// </summary>
    private async Task<IReadOnlyList<BindingKandidat>> LoadBindingKandidatiAsync(int zaznamId, CancellationToken ct)
    {
        var raw = await (
            from v in _db.VyjadreniVazby.AsNoTracking()
            where v.ZaznamId == zaznamId && v.Stav == (byte)VazbaStav.Active
            join eo in _db.ZaznamExterniOdkazy.AsNoTracking() on v.ExterniOdkazId equals eo.Id
            where eo.Cislo != null
            select new
            {
                v.ExterniOdkazId,
                eo.Cislo,
                Poradi = (int)v.Poradi,
                v.DatumVyjadreni,
                v.HotVyjadreniId,
            })
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        if (raw.Count == 0)
        {
            return Array.Empty<BindingKandidat>();
        }

        var cisla = raw.Select(r => r.Cislo!).Distinct().ToList();
        IReadOnlyDictionary<string, HotZaznamFingerprintDto> fp;
        try
        {
            fp = await _vyjadreniQuery.GetHotZaznamFingerprintsAsync(cisla, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "HarmonogramSkutecnostSyncService: fingerprint lookup selhal pro záznam {ZaznamId}.", zaznamId);
            fp = new Dictionary<string, HotZaznamFingerprintDto>();
        }

        var result = new List<BindingKandidat>(raw.Count);
        foreach (var r in raw)
        {
            if (!fp.TryGetValue(r.Cislo!, out var primary) || string.IsNullOrWhiteSpace(primary.TypZaznamu))
            {
                continue;
            }
            var typZaznamu = primary.TypZaznamu.Trim().ToUpperInvariant();
            var predikatKey = HarmonogramKrokDatumMapping.GetPredikatKey(typZaznamu, r.Poradi);
            if (predikatKey is null)
            {
                continue;
            }
            result.Add(new BindingKandidat(
                ExterniOdkazId: r.ExterniOdkazId,
                Cislo6: r.Cislo!,
                TypZaznamu: typZaznamu,
                PredikatKey: predikatKey,
                Datum: r.DatumVyjadreni,
                HotVyjadreniId: r.HotVyjadreniId));
        }
        return result;
    }
}
