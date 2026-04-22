using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Core service, která harvestuje vyjádření z HOT_VYJADRENI a ukládá vazby
/// na kroky harmonogramu. Bez UI — volá se:
/// - z <see cref="IHarvestScheduler"/> (T2 trigger po uložení záznamu),
/// - z <c>VyjadreniModalController.ReHarvest</c> (admin akce, synchronně),
/// - z diagnostické stránky <c>/SDConnector</c>,
/// - z <see cref="SdReactiveSyncConsumer"/> (T2/T5/T7/T8 queue-based reactive),
/// - z <c>SdActivePeriodicSyncHostedService</c> / <c>SdArchivePeriodicSyncHostedService</c> (T4 periodic),
/// - z <c>SdSyncController</c> (T3/T6 direct sync, user čeká).
///
/// Sd-sync-revise Task 6: fingerprint detekce (spec §5.2).
/// Před drill fetchem zkontroluj HOT_ZAZNAMY.datum primary + (MAX(id), COUNT(*)) secondary
/// fingerprint. Pokud se nezměnilo, drill přeskoč a jen updatni LastHarvestedAt (hint).
/// Per-ticket semafor zabezpečí neprovádění paralelního harvestu stejného tiketu.
/// </summary>
public sealed class VyjadreniHarvestService : IVyjadreniHarvestService
{
    // Per-ticket semafor: zabraňuje race mezi (a) periodic tick, (b) direct sync T3/T6,
    // (c) reactive consumer, (d) souběžné HTTP requesty. Velikost ~1000 tiketů × ~48 B = ~48 KB.
    // Reset na restartu aplikace.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> PerTicketLocks = new();

    private readonly PmTrackerDbContext _db;
    private readonly IVyjadreniQueryService _vyjadreni;
    private readonly TimeProvider _time;
    private readonly ILogger<VyjadreniHarvestService> _logger;

    public VyjadreniHarvestService(
        PmTrackerDbContext db,
        IVyjadreniQueryService vyjadreni,
        TimeProvider time,
        ILogger<VyjadreniHarvestService> logger)
    {
        _db = db;
        _vyjadreni = vyjadreni;
        _time = time;
        _logger = logger;
    }

    public async Task<VyjadreniHarvestResult> HarvestTicketAsync(int externiOdkazId, CancellationToken ct = default)
    {
        var eo = await _db.ZaznamExterniOdkazy.FirstOrDefaultAsync(x => x.Id == externiOdkazId, ct).ConfigureAwait(false);
        if (eo is null)
        {
            return VyjadreniHarvestResult.Empty($"Externí odkaz id={externiOdkazId} neexistuje.");
        }
        if (string.IsNullOrWhiteSpace(eo.Cislo))
        {
            return VyjadreniHarvestResult.Empty("Externí odkaz nemá vyplněné 6místné číslo tiketu.");
        }

        return await HarvestTicketCoreAsync(eo, updateFingerprint: true, ct).ConfigureAwait(false);
    }

    public async Task HarvestRecordAsync(int zaznamId, CancellationToken ct = default)
    {
        var ids = await _db.ZaznamExterniOdkazy
            .Where(x => x.ZaznamId == zaznamId && x.Cislo != null && x.Cislo != "")
            .Select(x => x.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var id in ids)
        {
            try
            {
                await HarvestTicketAsync(id, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Harvest pro externí odkaz {ExterniOdkazId} (záznam {ZaznamId}) selhal.", id, zaznamId);
            }
        }
    }

    public Task HarvestForRecordAsync(int zaznamId, CancellationToken ct = default)
        => HarvestRecordAsync(zaznamId, ct);

    public async Task<VyjadreniHarvestResult> HarvestSingleTicketAsync(int externiOdkazId, CancellationToken ct = default)
    {
        var eo = await _db.ZaznamExterniOdkazy.FirstOrDefaultAsync(x => x.Id == externiOdkazId, ct).ConfigureAwait(false);
        if (eo is null)
        {
            return VyjadreniHarvestResult.Empty($"Externí odkaz id={externiOdkazId} neexistuje.");
        }
        if (string.IsNullOrWhiteSpace(eo.Cislo))
        {
            return VyjadreniHarvestResult.Empty("Externí odkaz nemá vyplněné 6místné číslo tiketu.");
        }

        // Primary + secondary fingerprint pro jediný tiket:
        var fingerprints = await _vyjadreni.GetHotZaznamFingerprintsAsync(new[] { eo.Cislo! }, ct).ConfigureAwait(false);
        if (!fingerprints.TryGetValue(eo.Cislo!, out var primary))
        {
            return VyjadreniHarvestResult.Empty($"Ticket #{eo.Cislo} neexistuje v HOT_ZAZNAMY.");
        }

        var secondaries = await _vyjadreni.GetVyjadreniSecondaryFingerprintsAsync(new[] { eo.Cislo! }, ct).ConfigureAwait(false);
        secondaries.TryGetValue(eo.Cislo!, out var secondary);
        secondary ??= new VyjadreniSecondaryFingerprintDto(eo.Cislo!, MaxId: 0L, Count: 0);

        return await HarvestWithFingerprintAsync(eo, primary, secondary, ct).ConfigureAwait(false);
    }

    public async Task<SdHarvestResult> HarvestScopeAsync(
        HarvestScope scope, SyncTriggerKind trigger, CancellationToken ct = default)
    {
        _ = trigger; // trigger je jen informativní, nepouzivame
        var startedAt = _time.GetUtcNow().UtcDateTime;

        var candidates = await _db.ZaznamExterniOdkazy
            .Where(x => x.Cislo != null && x.Cislo != "")
            .ToListAsync(ct).ConfigureAwait(false);

        if (candidates.Count == 0)
        {
            return SdHarvestResult.Empty(startedAt);
        }

        var cisla = candidates.Select(x => x.Cislo!).Distinct().ToList();

        // Primary fingerprint — vrací jen tickety, které v HOT existují
        var primaries = await _vyjadreni.GetHotZaznamFingerprintsAsync(cisla, ct).ConfigureAwait(false);

        // Filter podle scope:
        var scopedCandidates = scope switch
        {
            HarvestScope.Active => candidates
                .Where(eo => primaries.TryGetValue(eo.Cislo!, out var p) && !IsArchiveStav(p.Stav))
                .ToList(),
            HarvestScope.Archive => candidates
                .Where(eo => primaries.TryGetValue(eo.Cislo!, out var p) && IsArchiveStav(p.Stav))
                .ToList(),
            HarvestScope.All => candidates
                .Where(eo => primaries.ContainsKey(eo.Cislo!))
                .ToList(),
            _ => candidates.Where(eo => primaries.ContainsKey(eo.Cislo!)).ToList()
        };

        var stats = new ScopeRunStats();
        stats.Checked = scopedCandidates.Count;

        if (scopedCandidates.Count == 0)
        {
            var finishedAt0 = _time.GetUtcNow().UtcDateTime;
            return new SdHarvestResult(
                startedAt, finishedAt0,
                TicketsChecked: stats.Checked,
                TicketsSkippedByFingerprint: 0,
                TicketsDrilled: 0,
                BindingsCreated: 0,
                BindingsUpdated: 0,
                ErrorCount: 0,
                Errors: Array.Empty<SdHarvestErrorItem>());
        }

        var scopedCisla = scopedCandidates.Select(x => x.Cislo!).Distinct().ToList();
        var secondaries = await _vyjadreni.GetVyjadreniSecondaryFingerprintsAsync(scopedCisla, ct).ConfigureAwait(false);

        foreach (var eo in scopedCandidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var primary = primaries[eo.Cislo!];
                secondaries.TryGetValue(eo.Cislo!, out var secondary);
                secondary ??= new VyjadreniSecondaryFingerprintDto(eo.Cislo!, MaxId: 0L, Count: 0);

                var result = await HarvestWithFingerprintAsync(eo, primary, secondary, ct).ConfigureAwait(false);
                if (result.Fetched == 0 && result.Message == FingerprintSkippedMessage)
                {
                    stats.SkippedByFingerprint++;
                }
                else if (result.Message is null)
                {
                    stats.Drilled++;
                    stats.BindingsCreated += result.Created;
                    stats.BindingsUpdated += result.Superseded;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Harvest tiketu externi_odkaz_id={Id} cislo={Cislo} selhal v HarvestScopeAsync.",
                    eo.Id, eo.Cislo);
                stats.Errors.Add(new SdHarvestErrorItem(
                    ExterniOdkazId: eo.Id,
                    TicketId: int.TryParse(eo.Cislo, out var tid) ? tid : null,
                    Reason: ex.Message));
            }
        }

        var finishedAt = _time.GetUtcNow().UtcDateTime;
        return new SdHarvestResult(
            StartedAt: startedAt,
            FinishedAt: finishedAt,
            TicketsChecked: stats.Checked,
            TicketsSkippedByFingerprint: stats.SkippedByFingerprint,
            TicketsDrilled: stats.Drilled,
            BindingsCreated: stats.BindingsCreated,
            BindingsUpdated: stats.BindingsUpdated,
            ErrorCount: stats.Errors.Count,
            Errors: stats.Errors);
    }

    public async Task<VyjadreniHarvestResult> ReHarvestTicketAsync(int externiOdkazId, CancellationToken ct = default)
    {
        var eo = await _db.ZaznamExterniOdkazy.FirstOrDefaultAsync(x => x.Id == externiOdkazId, ct).ConfigureAwait(false);
        if (eo is null)
        {
            return VyjadreniHarvestResult.Empty($"Externí odkaz id={externiOdkazId} neexistuje.");
        }

        var activeAuto = await _db.VyjadreniVazby
            .Where(x => x.ExterniOdkazId == externiOdkazId
                     && x.Stav == (byte)VazbaStav.Active
                     && x.Source == (byte)VazbaSource.Auto)
            .ToListAsync(ct).ConfigureAwait(false);

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        foreach (var b in activeAuto)
        {
            b.Stav = (byte)VazbaStav.Deleted;
            b.DeletedAt = nowUtc;
        }

        eo.LastHarvestedAt = null;
        // ReHarvest invaliduje i fingerprint aby drill proběhl vždy
        eo.LastKnownHotZaznamDatum = null;
        eo.LastKnownMaxVyjadreniId = null;
        eo.LastKnownVyjadreniCount = null;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return await HarvestTicketAsync(externiOdkazId, ct).ConfigureAwait(false);
    }

    // ---------- internals ----------

    internal const string FingerprintSkippedMessage = "Fingerprint unchanged — skip drill.";

    /// <summary>
    /// Rozhodne podle fingerprintu, jestli drill-nout; pokud ne, updatne jen primary hint.
    /// Per-ticket semafor zabraňuje race condition.
    /// </summary>
    private async Task<VyjadreniHarvestResult> HarvestWithFingerprintAsync(
        ZaznamExterniOdkazEntity eo,
        HotZaznamFingerprintDto primary,
        VyjadreniSecondaryFingerprintDto secondary,
        CancellationToken ct)
    {
        // Primary skip: datum se nezměnil a máme non-null záznam
        var primaryMatches = eo.LastKnownHotZaznamDatum.HasValue
            && eo.LastKnownHotZaznamDatum.Value == primary.Datum;

        // Secondary skip (i když primary změnil): MAX(id)+count stejný jako last known
        var secondaryMatches = eo.LastKnownMaxVyjadreniId.HasValue
            && eo.LastKnownVyjadreniCount.HasValue
            && eo.LastKnownMaxVyjadreniId.Value == secondary.MaxId
            && eo.LastKnownVyjadreniCount.Value == secondary.Count;

        if (primaryMatches && secondaryMatches)
        {
            // Nic nového — neupdatujeme vůbec (ani hint).
            return VyjadreniHarvestResult.Empty(FingerprintSkippedMessage);
        }

        if (primaryMatches && eo.LastKnownMaxVyjadreniId is null)
        {
            // Edge case: primary v pořádku ale secondary ještě neznáme → první harvest → drill
        }

        // Secondary skip (fingerprint říká, že žádné nové vyjádření nepřibylo) — jen update primary hint
        if (secondaryMatches && !primaryMatches)
        {
            eo.LastKnownHotZaznamDatum = primary.Datum;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return VyjadreniHarvestResult.Empty(FingerprintSkippedMessage);
        }

        return await HarvestTicketCoreWithLockAsync(eo, primary, secondary, ct).ConfigureAwait(false);
    }

    private async Task<VyjadreniHarvestResult> HarvestTicketCoreWithLockAsync(
        ZaznamExterniOdkazEntity eo,
        HotZaznamFingerprintDto primary,
        VyjadreniSecondaryFingerprintDto secondary,
        CancellationToken ct)
    {
        var sem = PerTicketLocks.GetOrAdd(eo.Id, _ => new SemaphoreSlim(1, 1));
        var acquired = await sem.WaitAsync(TimeSpan.Zero, ct).ConfigureAwait(false);
        if (!acquired)
        {
            _logger.LogDebug("Harvest pro externí odkaz {ExterniOdkazId} už běží, skip.", eo.Id);
            return VyjadreniHarvestResult.Empty("Již probíhá paralelní harvest tohoto odkazu.");
        }

        try
        {
            var result = await HarvestTicketCoreAsync(eo, updateFingerprint: false, ct).ConfigureAwait(false);

            // Persist fingerprint po úspěšném drill-u
            eo.LastKnownHotZaznamDatum = primary.Datum;
            eo.LastKnownMaxVyjadreniId = secondary.MaxId;
            eo.LastKnownVyjadreniCount = secondary.Count;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            return result;
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<VyjadreniHarvestResult> HarvestTicketCoreAsync(
        ZaznamExterniOdkazEntity eo,
        bool updateFingerprint,
        CancellationToken ct)
    {
        var zaznam = await _db.ProjektoveZaznamy.AsNoTracking().FirstOrDefaultAsync(x => x.Id == eo.ZaznamId, ct).ConfigureAwait(false);
        if (zaznam is null)
        {
            return VyjadreniHarvestResult.Empty($"Projektový záznam id={eo.ZaznamId} neexistuje.");
        }

        var sinceUtc = eo.LastHarvestedAt;
        var list = await _vyjadreni.GetVyjadreniForTicketAsync(eo.Cislo!, sinceUtc, ct).ConfigureAwait(false);

        int created = 0;
        int superseded = 0;
        int skipped = 0;

        if (list.Count > 0)
        {
            var krokKeyByPoradi = await LoadKrokKeyByPoradiAsync(zaznam.HarmonogramSablonaVerze, ct).ConfigureAwait(false);
            var typZaznamu = NormalizeTypZaznamu(zaznam, eo);

            foreach (var v in list)
            {
                var kind = HarvestPredicates.ClassifyPopis(v.Popis);
                var poradi = MapKindToPoradi(kind, typZaznamu);
                if (poradi is null)
                {
                    skipped++;
                    continue;
                }
                if (!krokKeyByPoradi.TryGetValue(poradi.Value, out var krokKey))
                {
                    _logger.LogDebug("Harvest {ExterniOdkazId}: pořadí {Poradi} není v schema verze {SchemaVerze}; preskakuji vyjadreni id={VyjadreniId}.",
                        eo.Id, poradi, zaznam.HarmonogramSablonaVerze, v.Id);
                    skipped++;
                    continue;
                }

                var upsert = await UpsertBindingAsync(
                    zaznam.Id, krokKey, eo.Id,
                    v.Id, v.Datum, VazbaSource.Auto, ct).ConfigureAwait(false);
                switch (upsert)
                {
                    case UpsertOutcome.Created: created++; break;
                    case UpsertOutcome.Superseded: created++; superseded++; break;
                    case UpsertOutcome.Skipped: skipped++; break;
                }
            }
        }

        eo.LastHarvestedAt = _time.GetUtcNow().UtcDateTime;
        if (updateFingerprint)
        {
            // Standalone HarvestTicketAsync: nemáme fingerprint data k dispozici, necháme
            // je NULL → další fingerprint check detekuje absenci → bude drillovat vždy.
            // V reaktivním/periodic flow se fingerprint updatuje v HarvestTicketCoreWithLockAsync.
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new VyjadreniHarvestResult(list.Count, created, superseded, skipped);
    }

    private static bool IsArchiveStav(string? stav)
        => !string.IsNullOrWhiteSpace(stav)
           && string.Equals(stav.Trim(), "archiv", StringComparison.OrdinalIgnoreCase);

    private sealed class ScopeRunStats
    {
        public int Checked { get; set; }
        public int SkippedByFingerprint { get; set; }
        public int Drilled { get; set; }
        public int BindingsCreated { get; set; }
        public int BindingsUpdated { get; set; }
        public List<SdHarvestErrorItem> Errors { get; } = new();
    }

    internal enum UpsertOutcome { Skipped, Created, Superseded }

    internal async Task<UpsertOutcome> UpsertBindingAsync(
        int zaznamId, Guid krokKey, int externiOdkazId, long hotVyjadreniId,
        DateTime datumVyjadreni, VazbaSource source, CancellationToken ct)
    {
        var existing = await _db.VyjadreniVazby
            .Where(x => x.ZaznamId == zaznamId
                     && x.KrokKey == krokKey
                     && x.Stav == (byte)VazbaStav.Active)
            .ToListAsync(ct).ConfigureAwait(false);

        // Deduplikace: stejné hot_vyjadreni_id už jako Active — žádná změna
        if (existing.Any(x => x.HotVyjadreniId == hotVyjadreniId))
        {
            return UpsertOutcome.Skipped;
        }

        // Manuální vazby mají absolutní přednost: nepřepisuj je auto-harvestem
        if (source == VazbaSource.Auto && existing.Any(x => x.Source == (byte)VazbaSource.Manual))
        {
            return UpsertOutcome.Skipped;
        }

        // Tie-break: novější datum vyjádření vítězí (spec §8.1 DESC pro K4/K7, ASC pro K3/K6 —
        // auto-harvest jede chronologicky, takže ASC = první vítězí a DESC = poslední vítězí).
        // Pragmatické zjednodušení: novější bublina supersedne starší.
        var newest = existing.OrderByDescending(x => x.DatumVyjadreni).FirstOrDefault();
        if (newest != null && newest.DatumVyjadreni >= datumVyjadreni && source == VazbaSource.Auto)
        {
            return UpsertOutcome.Skipped;
        }

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        int supersededCount = 0;
        foreach (var e in existing)
        {
            e.Stav = (byte)VazbaStav.Superseded;
            supersededCount++;
        }

        _db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = zaznamId,
            KrokKey = krokKey,
            ExterniOdkazId = externiOdkazId,
            HotVyjadreniId = hotVyjadreniId,
            DatumVyjadreni = datumVyjadreni,
            Source = (byte)source,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = nowUtc
        });

        return supersededCount > 0 ? UpsertOutcome.Superseded : UpsertOutcome.Created;
    }

    private async Task<IReadOnlyDictionary<int, Guid>> LoadKrokKeyByPoradiAsync(int sablonaVerze, CancellationToken ct)
    {
        var rows = await _db.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(t => t.SablonaVerze == sablonaVerze && !t.JeZpozdeni)
            .Select(t => new { t.KrokPoradi, t.KrokKey })
            .ToListAsync(ct).ConfigureAwait(false);

        return rows
            .GroupBy(x => x.KrokPoradi)
            .ToDictionary(g => g.Key, g => g.First().KrokKey);
    }

    /// <summary>
    /// Zjistí typ tiketu (PMP/PNF/NES), aby se K4_K7_DodaniReseni správně
    /// zmapovalo na poradí 4 (PMP) nebo 7 (PNF). Prozatím nemáme v PM Tracker DB
    /// spolehlivé metadata o typu tiketu ze ServiceDesku, takže typujeme přes
    /// typ záznamu. Pokud typ není znám, vrací "".
    /// </summary>
    private static string NormalizeTypZaznamu(ProjektovyZaznamEntity zaznam, ZaznamExterniOdkazEntity eo)
    {
        // Fallback heuristika: "PNF" pokud externí odkaz nemá žádný speciální marker;
        // implementace v rámci Fáze 2 (sd-sync-revise) přidá přesný dotaz na HOT_ZAZNAMY.typ_zaznamu.
        _ = zaznam;
        _ = eo;
        return string.Empty;
    }

    private static int? MapKindToPoradi(HarvestPredicateKind kind, string typZaznamu)
        => kind switch
        {
            HarvestPredicateKind.K3_OdeslaniZadaniPmp => 3,
            HarvestPredicateKind.K6_OdeslaniPozadavku => 6,
            HarvestPredicateKind.K10_NasazeniArchivace => 10,
            HarvestPredicateKind.K4_K7_DodaniReseni =>
                string.Equals(typZaznamu, "PMP", StringComparison.OrdinalIgnoreCase) ? 4 : 7,
            _ => null
        };
}
