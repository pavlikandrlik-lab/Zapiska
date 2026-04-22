using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Core service, která harvestuje vyjádření z HOT_VYJADRENI a ukládá vazby
/// na kroky harmonogramu. Bez UI — volá se:
/// - z <see cref="IHarvestScheduler"/> (T2 trigger po uložení záznamu),
/// - z <c>VyjadreniModalController.ReHarvest</c> (admin akce, synchronně),
/// - z diagnostické stránky <c>/SDConnector</c>.
/// </summary>
public sealed class VyjadreniHarvestService : IVyjadreniHarvestService
{
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

        var zaznam = await _db.ProjektoveZaznamy.AsNoTracking().FirstOrDefaultAsync(x => x.Id == eo.ZaznamId, ct).ConfigureAwait(false);
        if (zaznam is null)
        {
            return VyjadreniHarvestResult.Empty($"Projektový záznam id={eo.ZaznamId} neexistuje.");
        }

        var sinceUtc = eo.LastHarvestedAt;
        var list = await _vyjadreni.GetVyjadreniForTicketAsync(eo.Cislo, sinceUtc, ct).ConfigureAwait(false);

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
                        externiOdkazId, poradi, zaznam.HarmonogramSablonaVerze, v.Id);
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
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new VyjadreniHarvestResult(list.Count, created, superseded, skipped);
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
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return await HarvestTicketAsync(externiOdkazId, ct).ConfigureAwait(false);
    }

    // ---------- internals ----------

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
