using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Plán D — čisté validace polí přidaných do návrhových payloadů:
/// <see cref="ManualActualKrokDto"/> (schémata 2 a 3) a
/// <see cref="HarmonogramVazbaDto"/> (schéma 3). Oddělená třída umožňuje
/// unit-testovat validační logiku bez DB kontextu.
/// </summary>
public static class ManualProposalFieldValidator
{
    /// <summary>
    /// Ruční skutečnost kroku 2/5/8/9:
    /// - <c>KrokKey</c> nesmí být <see cref="Guid.Empty"/>.
    /// - Žádné duplikáty (jeden krok = max jedno datum).
    /// - Datum nesmí být v budoucnosti (vůči <paramref name="today"/>).
    /// <para>
    /// Kontrola, že <c>KrokKey</c> skutečně patří do ručních kroků 2/5/8/9
    /// se dělá až na úrovni approve commandu, protože tam máme k dispozici
    /// schéma harmonogramu konkrétního záznamu.
    /// </para>
    /// </summary>
    public static void ValidateManualActualKroky(IReadOnlyList<ManualActualKrokDto> manualKroky, DateOnly today)
    {
        if (manualKroky.Count == 0)
        {
            return;
        }

        var seen = new HashSet<Guid>();
        foreach (var krok in manualKroky)
        {
            if (krok.KrokKey == Guid.Empty)
            {
                throw new InvalidOperationException("Ruční skutečnost kroku musí mít vyplněný identifikátor kroku.");
            }

            if (!seen.Add(krok.KrokKey))
            {
                throw new InvalidOperationException("Pro jeden krok harmonogramu lze zadat jen jedno ruční datum.");
            }

            if (krok.AbsolutniDatum > today)
            {
                throw new InvalidOperationException("Datum skutečnosti kroku nesmí být v budoucnosti.");
            }
        }
    }

    /// <summary>
    /// Pre-bound vazby bublina-&gt;krok (schéma 3 — CREATE_RECORD):
    /// - <c>KrokKey</c> nesmí být <see cref="Guid.Empty"/>.
    /// - <c>ExterniOdkazIndex</c> musí být v rozsahu [0, externiVazbyCount).
    /// - <c>HotVyjadreniId</c> musí být kladné.
    /// - Jeden krok = max jedna bublina (žádné duplikáty KrokKey).
    /// </summary>
    public static void ValidateHarmonogramVazby(IReadOnlyList<HarmonogramVazbaDto> vazby, int externiVazbyCount)
    {
        if (vazby.Count == 0)
        {
            return;
        }

        var seen = new HashSet<Guid>();
        foreach (var v in vazby)
        {
            if (v.KrokKey == Guid.Empty)
            {
                throw new InvalidOperationException("Vazba vyjádření na krok musí mít vyplněný identifikátor kroku.");
            }

            if (!seen.Add(v.KrokKey))
            {
                throw new InvalidOperationException("Pro jeden krok harmonogramu lze navést jen jednu bublinu.");
            }

            if (v.ExterniOdkazIndex < 0 || v.ExterniOdkazIndex >= externiVazbyCount)
            {
                throw new InvalidOperationException("Index externí vazby v návrhu je mimo rozsah.");
            }

            if (v.HotVyjadreniId <= 0)
            {
                throw new InvalidOperationException("Identifikátor vyjádření musí být kladné číslo.");
            }
        }
    }
}
