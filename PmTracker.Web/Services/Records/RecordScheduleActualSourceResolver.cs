using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Records;

public enum ZdrojSkutecnosti : byte
{
    None = 0,
    /// <summary>Skutečnost plyne z Active vazby v <c>zaznam_harmonogram_vyjadreni_vazba</c>.</summary>
    FromVyjadreni = 1,
    /// <summary>Skutečnost byla zadána ručně (HS0X_DELAY má hodnotu, ale vazba neexistuje).</summary>
    Manual = 2
}

public sealed record RecordScheduleActualSource(
    Guid KrokKey,
    int KrokPoradi,
    ZdrojSkutecnosti Zdroj,
    long? SourceVyjadreniId,
    DateTime? SourceVyjadreniDatum,
    int? SourceExterniOdkazId);

/// <summary>
/// Plán D Task 7 — pro každý krok harmonogramu záznamu určí, odkud pochází
/// skutečnost (vytěžené vyjádření vs. ruční zápis). UI (schedule panel)
/// podle toho rozhoduje, zda renderovat input box (ruční) nebo read-only
/// ikonu s odkazem na chat modal (vyjádření).
/// </summary>
public interface IRecordScheduleActualSourceResolver
{
    Task<IReadOnlyDictionary<Guid, RecordScheduleActualSource>> ResolveForRecordAsync(
        int zaznamId,
        int sablonaVerze,
        CancellationToken ct = default);
}

public sealed class RecordScheduleActualSourceResolver : IRecordScheduleActualSourceResolver
{
    private readonly PmTrackerDbContext _dbContext;

    public RecordScheduleActualSourceResolver(PmTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<Guid, RecordScheduleActualSource>> ResolveForRecordAsync(
        int zaznamId,
        int sablonaVerze,
        CancellationToken ct = default)
    {
        if (zaznamId <= 0)
        {
            return new Dictionary<Guid, RecordScheduleActualSource>();
        }

        var schemaRows = await _dbContext.CiselnikHarmonogramTypu
            .AsNoTracking()
            .Where(x => x.SablonaVerze == sablonaVerze)
            .Select(x => new { x.KrokKey, x.KrokPoradi, x.JeZpozdeni, x.Id })
            .ToListAsync(ct);

        var krokKeyToPoradi = schemaRows
            .GroupBy(x => x.KrokKey)
            .ToDictionary(g => g.Key, g => g.First().KrokPoradi);
        var delayTypIdByKrokKey = schemaRows
            .Where(x => x.JeZpozdeni)
            .GroupBy(x => x.KrokKey)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var activeBindings = await _dbContext.VyjadreniVazby
            .AsNoTracking()
            .Where(x => x.ZaznamId == zaznamId && x.Stav == (byte)VazbaStav.Active)
            .Select(x => new { x.KrokKey, x.HotVyjadreniId, x.DatumVyjadreni, x.ExterniOdkazId })
            .ToListAsync(ct);
        var activeByKrokKey = activeBindings
            .GroupBy(x => x.KrokKey)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DatumVyjadreni).First());

        var delayRows = await _dbContext.ZaznamHarmonogramHodnoty
            .AsNoTracking()
            .Where(x => x.ZaznamId == zaznamId)
            .Select(x => new { x.TypId, x.HodnotaInt })
            .ToListAsync(ct);
        var delayByTypId = delayRows.ToDictionary(x => x.TypId, x => x.HodnotaInt);

        var result = new Dictionary<Guid, RecordScheduleActualSource>();
        foreach (var krokKey in krokKeyToPoradi.Keys)
        {
            var poradi = krokKeyToPoradi[krokKey];

            if (activeByKrokKey.TryGetValue(krokKey, out var binding))
            {
                result[krokKey] = new RecordScheduleActualSource(
                    krokKey,
                    poradi,
                    ZdrojSkutecnosti.FromVyjadreni,
                    binding.HotVyjadreniId,
                    binding.DatumVyjadreni,
                    binding.ExterniOdkazId);
                continue;
            }

            // FIX 2026-05-01 (DESIGN-10-A): hodnota je int? (nullable). Po Phase 1.5 refaktoru
            // může být NULL = "krok nenastal" → NESMÍ se interpretovat jako Manual source.
            // Před fixem: `hodnota != 0` vrátilo TRUE i pro NULL (null != 0 == true v C#),
            // takže krok bez záznamu se chybně označil jako Manual.
            if (delayTypIdByKrokKey.TryGetValue(krokKey, out var delayTypId)
                && delayByTypId.TryGetValue(delayTypId, out var hodnota)
                && hodnota.HasValue
                && hodnota.Value != 0)
            {
                result[krokKey] = new RecordScheduleActualSource(
                    krokKey,
                    poradi,
                    ZdrojSkutecnosti.Manual,
                    null,
                    null,
                    null);
                continue;
            }

            result[krokKey] = new RecordScheduleActualSource(
                krokKey,
                poradi,
                ZdrojSkutecnosti.None,
                null,
                null,
                null);
        }

        return result;
    }
}
