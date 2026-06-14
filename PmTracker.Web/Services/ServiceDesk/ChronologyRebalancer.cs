namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Snapshot stavu jednoho kroku stepperu — co je aktuálně navázáno.
/// </summary>
public sealed record StepperKrok(
    int KrokPoradi,
    DateTime? CurrentBubbleDatum,
    long? CurrentBubbleId);

/// <summary>Dostupná bublina (vyjádření), která se potenciálně dá připnout na krok.</summary>
public sealed record BubleId(long Id, DateTime Datum);

/// <summary>Jeden update — "krok X má nyní bublinu Y (nebo null = buffer)".</summary>
public sealed record RebalanceUpdate(int KrokPoradi, long? NewBubbleId);

public sealed record RebalanceResult(IReadOnlyList<RebalanceUpdate> Updates);

/// <summary>
/// Chronologická rebalance — když uživatel (nebo auto-harvest) naváže bublinu
/// s datem D na krok K, musí následující kroky respektovat K.datum ≤ K+1.datum.
/// Pokud následující krok má dřívější bublinu, hledá se nahrazení v
/// <c>availableBubbles</c>; pokud nic vhodného není, krok jde do bufferu (NewBubbleId = null).
/// </summary>
/// <remarks>
/// Algoritmus je čistá funkce — nedělá DB volání. Použije se:
/// - Server-side: <see cref="VyjadreniHarvestService"/> při manual drag-and-drop
///   přes <c>VyjadreniModalController</c>.
/// - Client-side: JS preview před odesláním (mirror implementace).
/// </remarks>
public static class ChronologyRebalancer
{
    public static RebalanceResult Rebalance(
        IReadOnlyList<StepperKrok> kroky,
        int targetKrokPoradi,
        long newBubbleId,
        DateTime newBubbleDatum,
        IReadOnlyList<BubleId> availableBubbles)
    {
        ArgumentNullException.ThrowIfNull(kroky);
        ArgumentNullException.ThrowIfNull(availableBubbles);

        var updates = new List<RebalanceUpdate>();
        var usedBubbleIds = kroky
            .Where(k => k.CurrentBubbleId.HasValue)
            .Select(k => k.CurrentBubbleId!.Value)
            .ToHashSet();
        usedBubbleIds.Remove(newBubbleId);

        var sortedKroky = kroky.OrderBy(k => k.KrokPoradi).ToList();
        DateTime? minDatumProNasledujici = newBubbleDatum;

        foreach (var k in sortedKroky)
        {
            if (k.KrokPoradi < targetKrokPoradi) continue;

            if (k.KrokPoradi == targetKrokPoradi)
            {
                updates.Add(new RebalanceUpdate(k.KrokPoradi, newBubbleId));
                minDatumProNasledujici = newBubbleDatum;
                continue;
            }

            // Následující krok — pokud jeho současná bublina je po minimu, chronologie OK
            if (k.CurrentBubbleDatum.HasValue && k.CurrentBubbleDatum.Value > minDatumProNasledujici)
            {
                minDatumProNasledujici = k.CurrentBubbleDatum.Value;
                continue;
            }

            var candidate = availableBubbles
                .Where(b => b.Datum > minDatumProNasledujici && !usedBubbleIds.Contains(b.Id))
                .OrderBy(b => b.Datum)
                .FirstOrDefault();

            if (candidate is not null)
            {
                updates.Add(new RebalanceUpdate(k.KrokPoradi, candidate.Id));
                minDatumProNasledujici = candidate.Datum;
                usedBubbleIds.Add(candidate.Id);
            }
            else
            {
                // Parking / buffer — krok nemá vhodnou bublinu, půjde ručně
                updates.Add(new RebalanceUpdate(k.KrokPoradi, null));
            }
        }

        return new RebalanceResult(updates);
    }
}
