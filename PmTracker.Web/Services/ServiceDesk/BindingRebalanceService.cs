using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Request pro vytvoření nové Active vazby přes manual drag-and-drop na stepper.
/// </summary>
/// <param name="ZaznamId">ID projektového záznamu.</param>
/// <param name="ExterniOdkazId">ID externí vazby (tiket).</param>
/// <param name="KrokKey">Guid identifikátor cílového kroku harmonogramu.</param>
/// <param name="HotVyjadreniId">ID bubliny z HOT_VYJADRENI.</param>
/// <param name="DatumVyjadreni">Datum bubliny (pro chronologie check).</param>
/// <param name="OsobaId">Autor akce (záznam do <c>created_by_osoba_id</c>).</param>
public sealed record BindingRebalanceRequest(
    int ZaznamId,
    int ExterniOdkazId,
    Guid KrokKey,
    long HotVyjadreniId,
    DateTime DatumVyjadreni,
    int? OsobaId);

/// <summary>
/// Výsledek jedné cascade operace — pro klienta, aby uměl re-render stepperu bez full refresh.
/// </summary>
/// <param name="KrokKey">Cílový krok (Guid).</param>
/// <param name="KrokPoradi">Pořadí 1..10 — klient může refreshnout konkrétní box.</param>
/// <param name="NewVazbaId">ID nově vytvořené Active vazby (null = krok je prázdný / v bufferu).</param>
/// <param name="NewHotVyjadreniId">ID nové bubliny (null = buffer).</param>
/// <param name="SupersededVazbaIds">ID vazeb, které byly supersedované.</param>
public sealed record BindingCascadeUpdate(
    Guid KrokKey,
    int KrokPoradi,
    int? NewVazbaId,
    long? NewHotVyjadreniId,
    IReadOnlyList<int> SupersededVazbaIds);

public enum BindingRebalanceOutcome
{
    Success = 0,
    ExterniOdkazNotFound = 1,
    InvalidKrokKey = 2
}

public sealed record BindingRebalanceResult(
    BindingRebalanceOutcome Outcome,
    int? PrimaryVazbaId,
    IReadOnlyList<BindingCascadeUpdate> CascadeUpdates);

public interface IBindingRebalanceService
{
    /// <summary>
    /// Vytvoří novou Active vazbu na zadaný krok + aplikuje chronologickou rebalance
    /// na následující kroky (cascade updates). Celé pod 1 transakcí pod per-externiOdkazId
    /// semaforem (sdílený s <c>VyjadreniHarvestService</c>).
    /// </summary>
    Task<BindingRebalanceResult> CreateBindingAsync(
        BindingRebalanceRequest request,
        CancellationToken ct);
}

/// <summary>
/// Aplikuje <see cref="ChronologyRebalancer"/> do DB. Volá se z
/// <c>VyjadreniModalController.CreateVazba</c> (POST /Vyjadreni/HarmonogramVazba/Create).
///
/// Semantika:
/// 1. Načte aktuální Active vazby pro (ZaznamId, ExterniOdkazId) a všechny duration
///    kroky schématu záznamu.
/// 2. Načte dostupné bubliny přes <see cref="IVyjadreniQueryService"/>.
/// 3. Zavolá čistou funkci <see cref="ChronologyRebalancer.Rebalance"/>.
/// 4. Aplikuje updates: target krok dostane <see cref="VazbaSource.Manual"/>,
///    následné (cascade) kroky dostanou <see cref="VazbaSource.ChronologyCascade"/>.
/// 5. Původní Active vazby se supersednou. Vše v jedné transakci pod per-externiOdkaz lockem.
/// </summary>
public sealed class BindingRebalanceService : IBindingRebalanceService
{
    private readonly PmTrackerDbContext _db;
    private readonly IVyjadreniQueryService _vyjadreni;
    private readonly TimeProvider _time;
    private readonly ILogger<BindingRebalanceService> _logger;

    public BindingRebalanceService(
        PmTrackerDbContext db,
        IVyjadreniQueryService vyjadreni,
        TimeProvider time,
        ILogger<BindingRebalanceService> logger)
    {
        _db = db;
        _vyjadreni = vyjadreni;
        _time = time;
        _logger = logger;
    }

    public async Task<BindingRebalanceResult> CreateBindingAsync(
        BindingRebalanceRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Load externí odkaz a záznam (bez tracking — potřebujeme jen číst).
        var eo = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ExterniOdkazId && x.ZaznamId == request.ZaznamId, ct)
            .ConfigureAwait(false);
        if (eo is null)
        {
            return Fail(BindingRebalanceOutcome.ExterniOdkazNotFound);
        }

        var zaznam = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == request.ZaznamId)
            .Select(x => new { x.Id, x.HarmonogramSablonaVerze })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (zaznam is null)
        {
            return Fail(BindingRebalanceOutcome.ExterniOdkazNotFound);
        }

        // Všechny duration kroky daného schématu.
        var schemaKroky = await _db.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(t => t.SablonaVerze == zaznam.HarmonogramSablonaVerze && !t.JeZpozdeni)
            .Select(t => new { t.KrokKey, t.KrokPoradi })
            .ToListAsync(ct).ConfigureAwait(false);

        var targetKrok = schemaKroky.FirstOrDefault(k => k.KrokKey == request.KrokKey);
        if (targetKrok is null)
        {
            return Fail(BindingRebalanceOutcome.InvalidKrokKey);
        }

        // Lock + transakce. Per-externiOdkaz semafor zabraňuje race s auto-harvestem.
        // HOT_VYJADRENI fetch se dělá AŽ UVNITŘ locku (M-S2-1 fix) — jinak by paralelní
        // auto-harvest mohl v okně mezi fetchem a acquire-lockem přihodit nové bubliny,
        // které by cascade rebalance neviděl (stale availableBubbles) a mohl by
        // přepsat právě vytvořené Auto bindings.
        var sem = PerExterniOdkazLockRegistry.GetOrAdd(request.ExterniOdkazId);
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Dostupné bubliny z HOT_VYJADRENI uvnitř locku — read-only fetch.
            // Primární case: user drag-nul z bublinové timeline → bublina musí
            // existovat v availableBubbles. Kdyby HOT byl offline a fetch vrátí prázdno,
            // rebalance běží aspoň na aktuálním state-u a target binding se vytvoří
            // (bez cascade).
            IReadOnlyList<HotVyjadreniDto> hotList;
            if (!string.IsNullOrWhiteSpace(eo.Cislo))
            {
                try
                {
                    hotList = await _vyjadreni.GetVyjadreniForTicketAsync(eo.Cislo!, null, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "BindingRebalanceService: HOT fetch pro tiket {Cislo} selhal; cascade poběží s prázdným availableBubbles.",
                        eo.Cislo);
                    hotList = Array.Empty<HotVyjadreniDto>();
                }
            }
            else
            {
                hotList = Array.Empty<HotVyjadreniDto>();
            }

            return await ExecuteUnderTransactionAsync(request, zaznam.HarmonogramSablonaVerze, schemaKroky.Select(k => (k.KrokKey, k.KrokPoradi)).ToArray(), targetKrok.KrokPoradi, hotList, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<BindingRebalanceResult> ExecuteUnderTransactionAsync(
        BindingRebalanceRequest request,
        int sablonaVerze,
        IReadOnlyList<(Guid KrokKey, int KrokPoradi)> schemaKroky,
        int targetKrokPoradi,
        IReadOnlyList<HotVyjadreniDto> hotList,
        CancellationToken ct)
    {
        _ = sablonaVerze; // ponechano pro budoucí rozšíření (např. diagnostika log)

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

            // Active vazby scoped na (ZaznamId, ExterniOdkazId) — H-2 regression
            // pattern: nikdy nemutujeme bindingy jiného externího odkazu téhož záznamu.
            var activeBindings = await _db.VyjadreniVazby
                .Where(v => v.ZaznamId == request.ZaznamId
                         && v.ExterniOdkazId == request.ExterniOdkazId
                         && v.Stav == (byte)VazbaStav.Active)
                .ToListAsync(ct).ConfigureAwait(false);
            var bindingByKey = activeBindings
                .GroupBy(b => b.KrokKey)
                .ToDictionary(g => g.Key, g => g.First());

            // Mapování KrokKey → KrokPoradi pro cascade update assembly.
            var poradiByKey = schemaKroky.ToDictionary(k => k.KrokKey, k => k.KrokPoradi);

            // Stepper snapshot pro rebalancer.
            var stepperKroky = schemaKroky
                .Select(k =>
                {
                    bindingByKey.TryGetValue(k.KrokKey, out var b);
                    return new StepperKrok(
                        k.KrokPoradi,
                        k.KrokKey,
                        CurrentBubbleDatum: b?.DatumVyjadreni,
                        CurrentBubbleId: b?.HotVyjadreniId);
                })
                .ToArray();

            var availableBubbles = hotList
                .Select(v => new BubleId(v.Id, v.Datum))
                .ToArray();

            var rebalance = ChronologyRebalancer.Rebalance(
                stepperKroky,
                targetKrokPoradi,
                request.HotVyjadreniId,
                request.DatumVyjadreni,
                availableBubbles);

            var nowUtc = _time.GetUtcNow().UtcDateTime;
            int? primaryVazbaId = null;
            var cascadeUpdates = new List<BindingCascadeUpdate>();
            // entity → (isTarget, krokKey, krokPoradi) — pro post-SaveChanges ID fill.
            var pendingEntities = new Dictionary<ZaznamHarmonogramVyjadreniVazbaEntity, (bool IsTarget, Guid KrokKey, int KrokPoradi)>();

            // Krok key → entity builder helper
            ZaznamHarmonogramVyjadreniVazbaEntity BuildEntity(Guid krokKey, long hotVyjadreniId, DateTime datum, VazbaSource source)
            {
                return new ZaznamHarmonogramVyjadreniVazbaEntity
                {
                    ZaznamId = request.ZaznamId,
                    ExterniOdkazId = request.ExterniOdkazId,
                    KrokKey = krokKey,
                    HotVyjadreniId = hotVyjadreniId,
                    DatumVyjadreni = datum,
                    Source = (byte)source,
                    Stav = (byte)VazbaStav.Active,
                    CreatedAt = nowUtc,
                    CreatedByOsobaId = request.OsobaId
                };
            }

            foreach (var update in rebalance.Updates)
            {
                var krokKey = schemaKroky.First(k => k.KrokPoradi == update.KrokPoradi).KrokKey;
                var isTarget = update.KrokPoradi == targetKrokPoradi;
                bindingByKey.TryGetValue(krokKey, out var existing);

                // Idempotence: re-drop téže bubliny na tentýž krok → no-op pro primary,
                // nebo cascade update, který by vedl k přesně stejnému bindingu, skip.
                if (update.NewBubbleId.HasValue && existing is not null
                    && existing.HotVyjadreniId == update.NewBubbleId.Value)
                {
                    if (isTarget)
                    {
                        primaryVazbaId = existing.Id;
                    }
                    continue;
                }

                // Buffer: cascade posun kroku, který nemá kandidáta → supersedneme stávající,
                // nic nového nevytvoříme (target tuhle větev nevejde — rebalancer vždy vrací
                // target s NewBubbleId = request.HotVyjadreniId).
                if (!update.NewBubbleId.HasValue)
                {
                    if (existing is not null)
                    {
                        existing.Stav = (byte)VazbaStav.Superseded;
                        cascadeUpdates.Add(new BindingCascadeUpdate(
                            KrokKey: krokKey,
                            KrokPoradi: update.KrokPoradi,
                            NewVazbaId: null,
                            NewHotVyjadreniId: null,
                            SupersededVazbaIds: new[] { existing.Id }));
                    }
                    continue;
                }

                // Change: supersedni stávající (pokud je) + přidej novou Active.
                var supersededIds = new List<int>();
                if (existing is not null)
                {
                    existing.Stav = (byte)VazbaStav.Superseded;
                    supersededIds.Add(existing.Id);
                }

                var newBubbleDatum = update.KrokPoradi == targetKrokPoradi
                    ? request.DatumVyjadreni
                    : availableBubbles.First(b => b.Id == update.NewBubbleId.Value).Datum;
                var source = isTarget ? VazbaSource.Manual : VazbaSource.ChronologyCascade;
                var entity = BuildEntity(krokKey, update.NewBubbleId.Value, newBubbleDatum, source);
                _db.VyjadreniVazby.Add(entity);

                // SaveChanges musíme volat až na konci; zatím držíme reference.
                // Cascade update přidáváme s placeholder VazbaId — vyplní se po SaveChanges.
                if (isTarget)
                {
                    // Primary slot — dopočítat po SaveChanges přes entity.Id
                }
                else
                {
                    cascadeUpdates.Add(new BindingCascadeUpdate(
                        KrokKey: krokKey,
                        KrokPoradi: update.KrokPoradi,
                        NewVazbaId: null, // fill after save
                        NewHotVyjadreniId: update.NewBubbleId,
                        SupersededVazbaIds: supersededIds));
                }
                // Track mapping new entity → update index for post-save ID fill
                pendingEntities[entity] = (isTarget, krokKey, update.KrokPoradi);
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            // Fill primary + cascade VazbaId po SaveChanges (entity.Id teď je alokované).
            foreach (var kv in pendingEntities)
            {
                var entity = kv.Key;
                var (isTarget, krokKey, krokPoradi) = kv.Value;
                if (isTarget)
                {
                    primaryVazbaId = entity.Id;
                }
                else
                {
                    // Najdi odpovídající cascade update a naplň ID.
                    for (int i = 0; i < cascadeUpdates.Count; i++)
                    {
                        if (cascadeUpdates[i].KrokKey == krokKey
                            && cascadeUpdates[i].KrokPoradi == krokPoradi
                            && cascadeUpdates[i].NewVazbaId is null
                            && cascadeUpdates[i].NewHotVyjadreniId == entity.HotVyjadreniId)
                        {
                            cascadeUpdates[i] = cascadeUpdates[i] with { NewVazbaId = entity.Id };
                            break;
                        }
                    }
                }
            }
            await tx.CommitAsync(ct).ConfigureAwait(false);

            return new BindingRebalanceResult(
                BindingRebalanceOutcome.Success,
                primaryVazbaId,
                cascadeUpdates);
        }).ConfigureAwait(false);
    }

    private static BindingRebalanceResult Fail(BindingRebalanceOutcome outcome)
        => new(outcome, PrimaryVazbaId: null, CascadeUpdates: Array.Empty<BindingCascadeUpdate>());
}
