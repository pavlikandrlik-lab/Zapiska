using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Schedules;
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
    // Per-ticket semafor registry je sdílený s BindingRebalanceService (manual drag-and-drop
    // rebalance), aby auto-harvest a manuální rebalance nemohly paralelně mutovat vazby
    // stejného tiketu. Viz <see cref="PerExterniOdkazLockRegistry"/>.

    private readonly PmTrackerDbContext _db;
    private readonly IVyjadreniQueryService _vyjadreni;
    private readonly TimeProvider _time;
    private readonly IPerExterniOdkazLockRegistry _lockRegistry;
    private readonly IHarmonogramSkutecnostSyncService? _skutecnostSync;
    private readonly IPerTicketMetadataSyncService? _metadataSync;
    private readonly ILogger<VyjadreniHarvestService> _logger;

    public VyjadreniHarvestService(
        PmTrackerDbContext db,
        IVyjadreniQueryService vyjadreni,
        TimeProvider time,
        IPerExterniOdkazLockRegistry lockRegistry,
        ILogger<VyjadreniHarvestService> logger,
        IHarmonogramSkutecnostSyncService? skutecnostSync = null,
        IPerTicketMetadataSyncService? metadataSync = null)
    {
        _db = db;
        _vyjadreni = vyjadreni;
        _time = time;
        _lockRegistry = lockRegistry;
        _skutecnostSync = skutecnostSync;
        _metadataSync = metadataSync;
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

        // Review finding Q-10: získej TypZaznamu z HOT_ZAZNAMY, aby se K4_K7_DodaniReseni
        // predikát správně namapoval na PMP=4 / PNF=7 (jinak default 7 pro všechno).
        // Spec 2026-04-28: navíc získej HOT_ZAZNAMY.datum pro synthetic K1 binding (krok 1).
        string? typZaznamu = null;
        DateTime? hotZaznamDatum = null;
        try
        {
            var fp = await _vyjadreni.GetHotZaznamFingerprintsAsync(new[] { eo.Cislo! }, ct).ConfigureAwait(false);
            if (fp.TryGetValue(eo.Cislo!, out var primary))
            {
                typZaznamu = primary.TypZaznamu;
                hotZaznamDatum = primary.Datum;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HarvestTicketAsync: TypZaznamu lookup failed for {Cislo}; fallback PNF mapping.", eo.Cislo);
        }

        // M-1: admin/standalone path (ReHarvestTicketAsync) musí jít přes stejný per-ticket
        // lock jako reactive/periodic cesta, jinak race s reactive harvesty téhož tiketu.
        return await ExecuteUnderTicketLockAsync(
            eo,
            ct2 => HarvestTicketCoreAsync(eo, typZaznamu, hotZaznamDatum, ct2),
            ct).ConfigureAwait(false);
    }

    private async Task<VyjadreniHarvestResult> ExecuteUnderTicketLockAsync(
        ZaznamExterniOdkazEntity eo,
        Func<CancellationToken, Task<VyjadreniHarvestResult>> work,
        CancellationToken ct)
    {
        var sem = _lockRegistry.GetOrAdd(eo.Id);
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await work(ct).ConfigureAwait(false);
        }
        finally
        {
            sem.Release();
        }
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
    /// Rozhodne podle fingerprintu, jestli drill-nout. Fast-path (oba matchují) je lock-free.
    /// Review finding C-6: Všechny zápisy (update primary hintu, drill + fingerprint persist)
    /// musí probíhat pod per-ticket semaforem, jinak race s paralelními harvesty.
    /// </summary>
    private async Task<VyjadreniHarvestResult> HarvestWithFingerprintAsync(
        ZaznamExterniOdkazEntity eo,
        HotZaznamFingerprintDto primary,
        VyjadreniSecondaryFingerprintDto secondary,
        CancellationToken ct)
    {
        // Fast-path (lock-free): pokud oba fingerprinty matchují, nic neděláme. Žádný zápis.
        var primaryMatches = eo.LastKnownHotZaznamDatum.HasValue
            && eo.LastKnownHotZaznamDatum.Value == primary.Datum;
        var secondaryMatches = eo.LastKnownMaxVyjadreniId.HasValue
            && eo.LastKnownVyjadreniCount.HasValue
            && eo.LastKnownMaxVyjadreniId.Value == secondary.MaxId
            && eo.LastKnownVyjadreniCount.Value == secondary.Count;

        if (primaryMatches && secondaryMatches)
        {
            return VyjadreniHarvestResult.Empty(FingerprintSkippedMessage);
        }

        // Slow-path: všechny zápisy pod per-ticket semaforem.
        return await HarvestTicketCoreWithLockAsync(eo, primary, secondary, ct).ConfigureAwait(false);
    }

    private async Task<VyjadreniHarvestResult> HarvestTicketCoreWithLockAsync(
        ZaznamExterniOdkazEntity eo,
        HotZaznamFingerprintDto primary,
        VyjadreniSecondaryFingerprintDto secondary,
        CancellationToken ct)
    {
        var sem = _lockRegistry.GetOrAdd(eo.Id);
        // M-2: Blokující WaitAsync(ct) místo WaitAsync(Zero). ReactiveSyncQueue dedup
        // brání pile-upu identických enqueue; legit re-drill pro stejný ticket musí
        // pockat, než uvolní lock, nikoli tiše drop (jinak NEW data z periody mezi
        // first a second požadavkem mizí do dalšího periodic ticku).
        await sem.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            // Re-read fingerprint fields pod lockem (DbContext používá change-tracked entitu;
            // re-read zajistí konzistentní rozhodnutí bez race s jiným requestem, který
            // mezitím mohl fingerprint updatovat a uvolnit lock).
            var primaryMatches = eo.LastKnownHotZaznamDatum.HasValue
                && eo.LastKnownHotZaznamDatum.Value == primary.Datum;
            var secondaryMatches = eo.LastKnownMaxVyjadreniId.HasValue
                && eo.LastKnownVyjadreniCount.HasValue
                && eo.LastKnownMaxVyjadreniId.Value == secondary.MaxId
                && eo.LastKnownVyjadreniCount.Value == secondary.Count;

            if (primaryMatches && secondaryMatches)
            {
                // Mezitím jiný consumer dohnal fingerprint; skip.
                return VyjadreniHarvestResult.Empty(FingerprintSkippedMessage);
            }

            // Secondary match but primary mismatch — jen update primary hintu.
            // Review finding C-6: zápis musí být uvnitř semaforu, jinak race.
            if (secondaryMatches && !primaryMatches)
            {
                eo.LastKnownHotZaznamDatum = primary.Datum;
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                return VyjadreniHarvestResult.Empty(FingerprintSkippedMessage);
            }

            // Full drill — fingerprint update delegováno do HarvestTicketCoreAsync (1 SaveChangesAsync).
            eo.LastKnownHotZaznamDatum = primary.Datum;
            eo.LastKnownMaxVyjadreniId = secondary.MaxId;
            eo.LastKnownVyjadreniCount = secondary.Count;
            // Q-10: TypZaznamu z primary fingerprintu pro správné K4/K7 mapování.
            // Spec 2026-04-28: primary.Datum se předává jako hint pro synthetic K1 binding.
            return await HarvestTicketCoreAsync(eo, primary.TypZaznamu, primary.Datum, ct).ConfigureAwait(false);
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<VyjadreniHarvestResult> HarvestTicketCoreAsync(
        ZaznamExterniOdkazEntity eo,
        string? typZaznamuHint,
        DateTime? hotZaznamDatumHint,
        CancellationToken ct)
    {
        var zaznam = await _db.ProjektoveZaznamy.AsNoTracking().FirstOrDefaultAsync(x => x.Id == eo.ZaznamId, ct).ConfigureAwait(false);
        if (zaznam is null)
        {
            return VyjadreniHarvestResult.Empty($"Projektový záznam id={eo.ZaznamId} neexistuje.");
        }

        // Spec 2026-04-28: NES vazby jsou odpojené od harmonogramu. Žádný harvest stepper
        // bindings, žádná synthetic K1. Pouze metadata sync (4 datumy na kartě externí vazby).
        var typZaznamu = typZaznamuHint ?? NormalizeTypZaznamu(zaznam, eo);
        var isNes = string.Equals(typZaznamu, "NES", StringComparison.OrdinalIgnoreCase);

        var sinceUtc = eo.LastHarvestedAt;
        var list = isNes
            ? Array.Empty<HotVyjadreniDto>()  // NES neharvestuje stepper bindings
            : await _vyjadreni.GetVyjadreniForTicketAsync(eo.Cislo!, sinceUtc, ct).ConfigureAwait(false);

        int created = 0;
        int superseded = 0;
        int skipped = 0;

        if (!isNes)
        {
            var krokKeyByPoradi = await LoadKrokKeyByPoradiAsync(zaznam.HarmonogramSablonaVerze, ct).ConfigureAwait(false);

            // Review finding P-1: pre-load aktivní vazby v 1 query, ať UpsertBindingInMemory
            // nemusí per-bublina dělat samostatný .Where(...).ToListAsync.
            // H-2 regression fix: scope pre-load na tento ticket (ExterniOdkazId == eo.Id),
            // aby dvě paralelní harvesty pro různé externí odkazy téhož záznamu neracovaly na
            // sdíleném change trackeru — per-ticket SemaphoreSlim je keyed by externiOdkazId,
            // ne by zaznamId, takže scope musí zůstat per-ticket.
            var allActiveBindings = await _db.VyjadreniVazby
                .Where(x => x.ZaznamId == zaznam.Id
                         && x.ExterniOdkazId == eo.Id
                         && x.Stav == (byte)VazbaStav.Active)
                .ToListAsync(ct).ConfigureAwait(false);
            var bindingsByKey = allActiveBindings
                .GroupBy(b => b.KrokKey)
                .ToDictionary(g => g.Key, g => g.ToList());

            if (list.Count > 0)
            {
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

                    if (!bindingsByKey.TryGetValue(krokKey, out var existingForKey))
                    {
                        existingForKey = new List<ZaznamHarmonogramVyjadreniVazbaEntity>();
                        bindingsByKey[krokKey] = existingForKey;
                    }

                    var upsert = UpsertBindingInMemory(
                        zaznam.Id, krokKey, eo.Id,
                        v.Id, v.Datum, VazbaSource.Auto, existingForKey);
                    switch (upsert)
                    {
                        case UpsertOutcome.Created: created++; break;
                        case UpsertOutcome.Superseded: created++; superseded++; break;
                        case UpsertOutcome.Skipped: skipped++; break;
                    }
                }
            }

            // Spec 2026-04-28: synthetic K1 binding pro PMP/PNF.
            // Krok 1 „příprava zadání dodavateli" se plní z HOT_ZAZNAMY.datum (datum založení tiketu),
            // ne z popisu vyjádření. HotVyjadreniId = 0 je rezervované pro synthetic bindings
            // (žádná FK na HOT_VYJADRENI v PM Tracker DB).
            if (hotZaznamDatumHint.HasValue
                && krokKeyByPoradi.TryGetValue(1, out var k1KrokKey))
            {
                if (!bindingsByKey.TryGetValue(k1KrokKey, out var existingK1))
                {
                    existingK1 = new List<ZaznamHarmonogramVyjadreniVazbaEntity>();
                    bindingsByKey[k1KrokKey] = existingK1;
                }

                var k1Outcome = UpsertBindingInMemory(
                    zaznam.Id, k1KrokKey, eo.Id,
                    hotVyjadreniId: 0L, // synthetic
                    datumVyjadreni: hotZaznamDatumHint.Value,
                    VazbaSource.Auto, existingK1);
                switch (k1Outcome)
                {
                    case UpsertOutcome.Created: created++; break;
                    case UpsertOutcome.Superseded: created++; superseded++; break;
                    case UpsertOutcome.Skipped: /* deduplikováno (datum se nezměnil) */ break;
                }
            }
        }

        eo.LastHarvestedAt = _time.GetUtcNow().UtcDateTime;
        // Fingerprint update provádí HarvestTicketCoreWithLockAsync (reaktivní/periodic
        // cesta). Standalone HarvestTicketAsync / ReHarvestTicketAsync fingerprint
        // záměrně nenastavuje — následující fingerprint check detekuje NULL a vynutí
        // další drill. Q-9: dead `if (updateFingerprint)` branch odstraněn.
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Plán 4 Feature C Task 5 — po harvest batch commitu zavolej SkutecnostSync
        // aby Zdroj/Preferred na HS0X_DELAY řádcích reflektoval nové / změněné bindings.
        // Best-effort: chyby syncu nezasahují do výsledku harvestu (fingerprinty jsou commited).
        if (_skutecnostSync is not null && (created > 0 || superseded > 0))
        {
            try
            {
                await _skutecnostSync.SyncZaznamAsync(zaznam.Id, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "VyjadreniHarvestService: SkutecnostSync selhal pro záznam {ZaznamId} po harvestu ticketu {Cislo}.",
                    zaznam.Id, eo.Cislo);
            }
        }

        // Spec 2026-04-28: per-ticket metadata sync (4 datumy) — volá se pro VŠECHNY typy
        // vč. NES. Best-effort: chyba neblokuje výsledek harvestu.
        if (_metadataSync is not null)
        {
            try
            {
                await _metadataSync.SyncTicketAsync(eo.Id, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "VyjadreniHarvestService: PerTicketMetadataSync selhal pro externí odkaz {Id} (cislo={Cislo}).",
                    eo.Id, eo.Cislo);
            }
        }

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

    /// <summary>
    /// Review finding P-1: In-memory varianta upsert logiky. Volající pre-loaduje
    /// všechny aktivní vazby (1× SELECT na celý tiket, ne per-bublina) a předává
    /// per-krokKey subset. Metoda mutuje <paramref name="existingForKey"/> —
    /// supersedované entity se označí v EF change trackeru, nové přidají do DbSetu
    /// a do seznamu. Single SaveChangesAsync dělá caller.
    /// </summary>
    internal UpsertOutcome UpsertBindingInMemory(
        int zaznamId, Guid krokKey, int externiOdkazId, long hotVyjadreniId,
        DateTime datumVyjadreni, VazbaSource source,
        List<ZaznamHarmonogramVyjadreniVazbaEntity> existingForKey)
    {
        // Deduplikace: stejné hot_vyjadreni_id už jako Active — žádná změna
        if (existingForKey.Any(x => x.HotVyjadreniId == hotVyjadreniId))
        {
            return UpsertOutcome.Skipped;
        }

        // Manuální vazby mají absolutní přednost: nepřepisuj je auto-harvestem
        if (source == VazbaSource.Auto && existingForKey.Any(x => x.Source == (byte)VazbaSource.Manual))
        {
            return UpsertOutcome.Skipped;
        }

        // Tie-break: novější datum vyjádření vítězí. Auto-harvest jede chronologicky ASC,
        // takže už navázané bubliny s datem >= aktuálního data jsou "novější".
        var newest = existingForKey.OrderByDescending(x => x.DatumVyjadreni).FirstOrDefault();
        if (newest != null && newest.DatumVyjadreni >= datumVyjadreni && source == VazbaSource.Auto)
        {
            return UpsertOutcome.Skipped;
        }

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        int supersededCount = 0;
        foreach (var e in existingForKey)
        {
            e.Stav = (byte)VazbaStav.Superseded;
            supersededCount++;
        }
        // Odstraň supersedované z lookupu, aby další iterace pro stejný KrokKey
        // neviděly "aktivní" vazby, které už aktivní nejsou.
        existingForKey.Clear();

        var entity = new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = zaznamId,
            KrokKey = krokKey,
            ExterniOdkazId = externiOdkazId,
            HotVyjadreniId = hotVyjadreniId,
            DatumVyjadreni = datumVyjadreni,
            Source = (byte)source,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = nowUtc
        };
        _db.VyjadreniVazby.Add(entity);
        existingForKey.Add(entity);

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
    /// Fallback heuristika pokud caller nepředal <c>typZaznamuHint</c> z HOT_ZAZNAMY —
    /// vrací prázdný string, což v <see cref="MapKindToPoradi"/> vede k PNF (poradí 7).
    /// Primární zdroj je <c>HotZaznamFingerprintDto.TypZaznamu</c> — review finding Q-10.
    /// </summary>
    private static string NormalizeTypZaznamu(ProjektovyZaznamEntity zaznam, ZaznamExterniOdkazEntity eo)
    {
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
