using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Pure-logic validátor dvojfrázové podmínky pro vytěžení <c>PlanDodani</c> z PMP/PNF tiketů.
///
/// Algoritmus (dle spec 2026-04-28-nes-vyjadreni-a-4-datumy-design §4.3):
/// <list type="number">
///   <item>Najdi vyjádření obsahující <em>PlanDodani frázi</em> („předal záznam dodavateli :"
///         AND „s termínem plnění dodavatele").</item>
///   <item>Pro každý takový kandidát <c>n</c> ověř, že vyjádření <c>n+1</c> nebo <c>n+2</c>
///         (chronologicky podle data) v tom samém tiketu obsahuje plnou K6 frázi
///         „Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.".</item>
///   <item>Vrať jen ty kandidáty, kde validace prošla.</item>
/// </list>
///
/// Bez K6 návazného vyjádření v okně n+1/n+2 se datum z PlanDodani textu <em>nesmí použít</em>
/// (může jít o avizovaný termín, který se nakonec nezrealizoval / kalkulace nebyla akceptována).
/// </summary>
public static class PlanDodaniDualPhraseValidator
{
    private const string PhrasePlanPartA = "předal záznam dodavateli :";
    private const string PhrasePlanPartB = "s termínem plnění dodavatele";
    private const string PhraseK6 = "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.";

    /// <summary>
    /// Z chronologicky seřazeného (ASC) seznamu vyjádření tiketu vrátí jen ty,
    /// které obsahují PlanDodani frázi a mají v okně n+1/n+2 návazné K6 vyjádření.
    /// </summary>
    public static IReadOnlyList<HotVyjadreniDto> FilterValidated(IReadOnlyList<HotVyjadreniDto> chronologicallyAscending)
    {
        if (chronologicallyAscending is null || chronologicallyAscending.Count == 0)
        {
            return Array.Empty<HotVyjadreniDto>();
        }

        var result = new List<HotVyjadreniDto>();
        for (int i = 0; i < chronologicallyAscending.Count; i++)
        {
            var v = chronologicallyAscending[i];
            if (!ContainsPlanDodaniPhrase(v.Popis))
            {
                continue;
            }

            // Validace: K6 fráze v n+1 nebo n+2.
            bool validated = false;
            for (int j = i + 1; j <= i + 2 && j < chronologicallyAscending.Count; j++)
            {
                if (ContainsK6Phrase(chronologicallyAscending[j].Popis))
                {
                    validated = true;
                    break;
                }
            }

            if (validated)
            {
                result.Add(v);
            }
        }

        return result;
    }

    private static bool ContainsPlanDodaniPhrase(string? popis)
    {
        if (string.IsNullOrWhiteSpace(popis)) return false;
        return popis.Contains(PhrasePlanPartA, StringComparison.OrdinalIgnoreCase)
            && popis.Contains(PhrasePlanPartB, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsK6Phrase(string? popis)
    {
        if (string.IsNullOrWhiteSpace(popis)) return false;
        return popis.Contains(PhraseK6, StringComparison.OrdinalIgnoreCase);
    }
}
