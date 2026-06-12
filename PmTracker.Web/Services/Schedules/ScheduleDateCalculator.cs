namespace PmTracker.Web.Services.Schedules;

/// <summary>Vstup jednoho kroku do datum-výpočtu. Plán i skutečnost jsou absolutní datumy (NULL = nevyplněno).</summary>
public sealed record ScheduleDateStep(int Poradi, DateTime? PlanDatum, DateTime? SkutecnostDatum);

/// <summary>Výsledek jednoho kroku — plánový segment a (jen u vyplněných) segment skutečnosti.</summary>
public sealed record ScheduleDateStepResult(
    int Poradi,
    DateTime PlanStart,
    DateTime PlanEnd,
    bool MaSkutecnost,
    DateTime SkutecnostStart,
    DateTime SkutecnostEnd);

/// <summary>Souhrn harmonogramu.</summary>
public sealed record ScheduleDateSummary(
    DateTime PlanoveDokonceni,
    DateTime SkutecneDokonceni,
    DateTime Termin,
    bool Stihame,
    int PrekroceniDni);

/// <summary>
/// Datum-model výpočtu harmonogramu (žádné offsety). Plán: segment kroku = [plan(k-1), plan(k)],
/// plan(0)=start. Skutečnost: jen vyplněné kroky; segment = [skut(předchozí vyplněný), skut(k)],
/// nevyplněné se nekreslí (pohltí je další vyplněný). Skutečné dokončení = poslední vyplněný;
/// pokud koncový krok nevyplněn → projekce na dnešek.
/// </summary>
public static class ScheduleDateCalculator
{
    public static IReadOnlyList<ScheduleDateStepResult> Compute(DateTime start, IReadOnlyList<ScheduleDateStep> steps)
    {
        var ordered = steps.OrderBy(x => x.Poradi).ToList();
        var results = new List<ScheduleDateStepResult>(ordered.Count);

        var planCursor = start.Date;
        var skutecnostCursor = start.Date; // konec posledního vyplněného kroku

        foreach (var step in ordered)
        {
            var planStart = planCursor;
            var planEnd = (step.PlanDatum?.Date) ?? planStart;
            if (planEnd < planStart)
            {
                planEnd = planStart;
            }
            planCursor = planEnd;

            bool maSkutecnost = step.SkutecnostDatum.HasValue;
            var skutecnostStart = skutecnostCursor;
            var skutecnostEnd = skutecnostCursor;
            if (maSkutecnost)
            {
                skutecnostEnd = step.SkutecnostDatum!.Value.Date;
                if (skutecnostEnd < skutecnostStart)
                {
                    skutecnostEnd = skutecnostStart; // nezáporná šířka (harvest mimo pořadí)
                }
                skutecnostCursor = skutecnostEnd;
            }

            results.Add(new ScheduleDateStepResult(
                step.Poradi, planStart, planEnd, maSkutecnost, skutecnostStart, skutecnostEnd));
        }

        return results;
    }

    public static ScheduleDateSummary Summarize(
        DateTime start,
        IReadOnlyList<ScheduleDateStep> steps,
        DateTime termin,
        DateTime today)
    {
        var ordered = steps.OrderBy(x => x.Poradi).ToList();
        var normalizedTermin = termin.Date;
        var normalizedToday = today.Date;

        var planoveDokonceni = ordered.Count == 0
            ? start.Date
            : Compute(start, ordered)[^1].PlanEnd;

        var posledniVyplneny = ordered.LastOrDefault(x => x.SkutecnostDatum.HasValue);
        var koncovyKrok = ordered.Count == 0 ? null : ordered[^1];
        var koncovyVyplnen = koncovyKrok?.SkutecnostDatum.HasValue == true;

        DateTime skutecneDokonceni;
        if (koncovyVyplnen)
        {
            skutecneDokonceni = koncovyKrok!.SkutecnostDatum!.Value.Date;
        }
        else
        {
            // koncový krok nevyplněn → projekce na dnešek (prodlení vůči aktuálnímu datu)
            var zaklad = posledniVyplneny?.SkutecnostDatum?.Date ?? start.Date;
            skutecneDokonceni = normalizedToday > zaklad ? normalizedToday : zaklad;
        }

        var stihame = skutecneDokonceni <= normalizedTermin;
        var prekroceni = stihame ? 0 : (skutecneDokonceni - normalizedTermin).Days;

        return new ScheduleDateSummary(planoveDokonceni, skutecneDokonceni, normalizedTermin, stihame, prekroceni);
    }
}
