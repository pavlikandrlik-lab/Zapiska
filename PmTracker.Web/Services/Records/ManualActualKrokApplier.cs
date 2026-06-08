using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Plán D — čistá konverze z absolutního kalendářního data skutečnosti
/// (<see cref="ManualActualKrokDto.AbsolutniDatum"/>) na odchylku v kalendářních
/// dnech vůči plánovanému konci kroku (<see cref="HarmonogramVypocetKroku.BaselineDatum"/>).
/// Uložení výsledku do <c>HS0X_DELAY.HodnotaInt</c> je v zodpovědnosti volajícího
/// (approve command v <c>RecordProposalService.DecisionCommands</c> nebo
/// direct save v <c>RecordService.SaveRecord</c> po Phase 4 / DESIGN-6-A).
/// </summary>
public static class ManualActualKrokApplier
{
    public sealed record ManualActualKrokApplied(
        Guid KrokKey,
        int KrokPoradi,
        int DelayTypId,
        int OdchylkaDni,
        DateOnly AbsolutniDatum);

    /// <summary>
    /// Spáruje ruční kroky s harmonogramovým schématem + vypočítanou timelinou
    /// (<see cref="HarmonogramVypocetKroku"/>) a vrátí list konverzí typu
    /// „pro krok X zapiš do DELAY (TypId) hodnotu odchylky D".
    ///
    /// <para>
    /// Validace, že <c>KrokKey</c> patří do sady ručních kroků 2/5/8/9, se
    /// provádí zde — jedná se o business rule, která přeberá přednost před
    /// textovou validací z <see cref="ManualProposalFieldValidator"/> (ta
    /// nemá přístup ke schématu).
    /// </para>
    /// </summary>
    /// <param name="manualKroky">Ruční skutečnosti z návrhového payloadu.</param>
    /// <param name="vypocet">
    /// Timeline záznamu (musí mít planEndDate pro všechny kroky, včetně zero-duration).
    /// </param>
    /// <param name="krokKeyToPoradi">
    /// Mapa <c>KrokKey -&gt; KrokPoradi</c> ze schématu harmonogramu daného záznamu.
    /// Nezbytné, protože <see cref="HarmonogramVypocetKroku"/> nenesou <c>KrokKey</c>.
    /// </param>
    /// <param name="krokKeyToDelayTypId">
    /// Mapa <c>KrokKey -&gt; DelayTypeId</c> — ID typového řádku
    /// <c>HS0X_DELAY</c> pro zápis do <c>ZaznamHarmonogramHodnoty</c>.
    /// </param>
    public static IReadOnlyList<ManualActualKrokApplied> Compute(
        IReadOnlyList<ManualActualKrokDto> manualKroky,
        IReadOnlyList<HarmonogramVypocetKroku> vypocet,
        IReadOnlyDictionary<Guid, int> krokKeyToPoradi,
        IReadOnlyDictionary<Guid, int> krokKeyToDelayTypId,
        bool acceptAutoEligibleKroky = false)
    {
        ArgumentNullException.ThrowIfNull(manualKroky);
        ArgumentNullException.ThrowIfNull(vypocet);
        ArgumentNullException.ThrowIfNull(krokKeyToPoradi);
        ArgumentNullException.ThrowIfNull(krokKeyToDelayTypId);

        if (manualKroky.Count == 0)
        {
            return Array.Empty<ManualActualKrokApplied>();
        }

        var vypocetByPoradi = vypocet.ToDictionary(x => x.KrokIndex, x => x);
        var result = new List<ManualActualKrokApplied>(manualKroky.Count);

        foreach (var mk in manualKroky)
        {
            // FIX 2026-05-02: AbsolutniDatum je nullable. NULL = "krok nenastal" → skip
            // (žádná DELAY hodnota se neaplikuje, krok zůstane v default stavu).
            if (!mk.AbsolutniDatum.HasValue)
            {
                continue;
            }

            if (!krokKeyToPoradi.TryGetValue(mk.KrokKey, out var poradi))
            {
                throw new InvalidOperationException(
                    $"Ruční skutečnost míří na krok (KrokKey={mk.KrokKey}), který není ve schématu harmonogramu tohoto záznamu.");
            }

            if (!HarmonogramManualSteps.IsManual(poradi) && !acceptAutoEligibleKroky)
            {
                throw new InvalidOperationException(
                    $"Krok {poradi} není mezi kroky s ruční skutečností (povoleny jsou 2, 5, 8, 9).");
            }

            if (!krokKeyToDelayTypId.TryGetValue(mk.KrokKey, out var delayTypId) || delayTypId <= 0)
            {
                throw new InvalidOperationException(
                    $"Krok {poradi} nemá ve schématu definovaný řádek pro odchylku (HS0X_DELAY).");
            }

            if (!vypocetByPoradi.TryGetValue(poradi, out var krokVypocet))
            {
                throw new InvalidOperationException(
                    $"V timeline záznamu chybí krok {poradi} — nelze spočítat odchylku.");
            }

            var planEnd = DateOnly.FromDateTime(krokVypocet.BaselineDatum);
            var absolutniDatum = mk.AbsolutniDatum!.Value; // null filtered above
            var odchylka = absolutniDatum.DayNumber - planEnd.DayNumber;

            result.Add(new ManualActualKrokApplied(
                mk.KrokKey,
                poradi,
                delayTypId,
                odchylka,
                absolutniDatum));
        }

        return result;
    }

    /// <summary>
    /// DESIGN-6-B (2026-05-01) — public method sdílená mezi approve flow
    /// (<c>RecordProposalService.DecisionCommands.ApproveProposalAsync</c>) a direct save flow
    /// (<c>RecordService.SaveRecord</c>, Phase 4 DESIGN-6-A).
    ///
    /// Resolves: schema KrokKey↔KrokPoradi a KrokKey↔DelayTypId, načte effective DURATION values
    /// (existing rows + submitted overrides), spočítá baseline timeline, pak deleguje na pure
    /// <see cref="Compute"/>. Žádný DB write — caller persistuje výsledek (UPSERT do
    /// <c>zaznam_harmonogram_hodnoty</c>).
    /// </summary>
    public static async Task<IReadOnlyList<ManualActualKrokApplied>> ApplyAsync(
        int zaznamId,
        IReadOnlyList<ManualActualKrokDto> manualKroky,
        HarmonogramSchemaDefinition schema,
        DateTime datumZalozeni,
        IReadOnlySet<int> plannedTypeIds,
        IReadOnlyList<SaveRecordHarmonogramValueCommand> submittedValues,
        PmTrackerDbContext dbContext,
        IHarmonogramService harmonogramService,
        CancellationToken ct,
        bool acceptAutoEligibleKroky = false)
    {
        if (manualKroky.Count == 0)
        {
            return Array.Empty<ManualActualKrokApplied>();
        }

        var krokKeyMeta = await dbContext.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(x => x.SablonaVerze == schema.Verze)
            .Select(x => new { x.KrokKey, x.KrokPoradi, x.JeZpozdeni, x.Id })
            .ToListAsync(ct).ConfigureAwait(false);

        var krokKeyToPoradi = krokKeyMeta
            .GroupBy(x => x.KrokKey)
            .ToDictionary(g => g.Key, g => g.First().KrokPoradi);
        var delayTypIdByKrokKey = krokKeyMeta
            .Where(x => x.JeZpozdeni)
            .GroupBy(x => x.KrokKey)
            .ToDictionary(g => g.Key, g => g.First().Id);

        // Fallback: pokud delay řádek má jiný KrokKey než duration (legacy data), spáruj přes KrokPoradi
        var delayByPoradi = krokKeyMeta
            .Where(x => x.JeZpozdeni)
            .GroupBy(x => x.KrokPoradi)
            .ToDictionary(g => g.Key, g => g.First().Id);
        foreach (var mk in manualKroky)
        {
            if (delayTypIdByKrokKey.ContainsKey(mk.KrokKey)) continue;
            if (!krokKeyToPoradi.TryGetValue(mk.KrokKey, out var poradi)) continue;
            if (delayByPoradi.TryGetValue(poradi, out var fallbackDelayId))
            {
                delayTypIdByKrokKey[mk.KrokKey] = fallbackDelayId;
            }
        }

        // Sestavit efektivní hodnoty trvání pro timeline: výchozí schéma + submitted override
        var submittedByType = submittedValues
            .Where(x => plannedTypeIds.Contains(x.TypId))
            .GroupBy(x => x.TypId)
            .ToDictionary(g => g.Key, g => Math.Max(0, g.Last().Hodnota));
        // FIX 2026-05-02: filter ZaznamId — bez něj query načte rows napříč všemi
        // záznamy se shodným TypId (TypId je sdílený PK ciselnik_harmonogram_typu, ne
        // per-record), ToDictionary by failnula s duplicate key. Tj. critical bug
        // způsoboval Save fail u CREATE i EDIT flow ve VŠECH tenant scenarios s 2+ records.
        // DESIGN-10-A: NULL DURATION → fallback 0 v dict.
        var existingDurations = (await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => x.ZaznamId == zaznamId && plannedTypeIds.Contains(x.TypId))
            .Select(x => new { x.TypId, x.HodnotaInt })
            .ToListAsync(ct).ConfigureAwait(false))
            .ToDictionary(x => x.TypId, x => x.HodnotaInt ?? 0);
        foreach (var kv in submittedByType)
        {
            existingDurations[kv.Key] = kv.Value;
        }

        var vypocet = harmonogramService.BuildHarmonogramVypocetPublic(
            datumZalozeni,
            schema.Kroky,
            existingDurations);

        return Compute(manualKroky, vypocet, krokKeyToPoradi, delayTypIdByKrokKey, acceptAutoEligibleKroky);
    }
}
