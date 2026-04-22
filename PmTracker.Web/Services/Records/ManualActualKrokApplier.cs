using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Plán D — čistá konverze z absolutního kalendářního data skutečnosti
/// (<see cref="ManualActualKrokDto.AbsolutniDatum"/>) na odchylku v kalendářních
/// dnech vůči plánovanému konci kroku (<see cref="HarmonogramVypocetKroku.BaselineDatum"/>).
/// Uložení výsledku do <c>HS0X_DELAY.HodnotaInt</c> je v zodpovědnosti volajícího
/// (approve command v <c>RecordProposalService.DecisionCommands</c>).
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
        IReadOnlyDictionary<Guid, int> krokKeyToDelayTypId)
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
            if (!krokKeyToPoradi.TryGetValue(mk.KrokKey, out var poradi))
            {
                throw new InvalidOperationException(
                    $"Ruční skutečnost míří na krok (KrokKey={mk.KrokKey}), který není ve schématu harmonogramu tohoto záznamu.");
            }

            if (!HarmonogramManualSteps.IsManual(poradi))
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
            var odchylka = mk.AbsolutniDatum.DayNumber - planEnd.DayNumber;

            result.Add(new ManualActualKrokApplied(
                mk.KrokKey,
                poradi,
                delayTypId,
                odchylka,
                mk.AbsolutniDatum));
        }

        return result;
    }
}
