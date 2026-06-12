using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Datum-model (2026-06-12) — ruční skutečnost kroku je přímo absolutní datum
/// (<see cref="ManualActualKrokDto.AbsolutniDatum"/>). Žádný offset/baseline — applier jen
/// validuje a vrací (Poradi, datum). Persistuje volající (UPSERT do <c>zaznam_harmonogram_krok</c>).
/// </summary>
public static class ManualActualKrokApplier
{
    public sealed record ManualActualKrokApplied(int Poradi, DateOnly AbsolutniDatum);

    /// <summary>
    /// Vyfiltruje ruční kroky s vyplněným datem a ověří, že jde o povolený krok
    /// (2/5/8/9, nebo libovolný auto-eligible pokud <paramref name="acceptAutoEligibleKroky"/>).
    /// </summary>
    public static IReadOnlyList<ManualActualKrokApplied> Compute(
        IReadOnlyList<ManualActualKrokDto> manualKroky,
        bool acceptAutoEligibleKroky = false)
    {
        ArgumentNullException.ThrowIfNull(manualKroky);
        if (manualKroky.Count == 0)
        {
            return Array.Empty<ManualActualKrokApplied>();
        }

        var result = new List<ManualActualKrokApplied>(manualKroky.Count);
        foreach (var mk in manualKroky)
        {
            if (!mk.AbsolutniDatum.HasValue)
            {
                continue; // NULL = krok nenastal
            }
            if (mk.Poradi is < 1 or > 10)
            {
                continue;
            }
            if (!HarmonogramManualSteps.IsManual(mk.Poradi) && !acceptAutoEligibleKroky)
            {
                throw new InvalidOperationException(
                    $"Krok {mk.Poradi} není mezi kroky s ruční skutečností (povoleny jsou 2, 5, 8, 9).");
            }
            result.Add(new ManualActualKrokApplied(mk.Poradi, mk.AbsolutniDatum.Value));
        }

        return result;
    }
}
