namespace PmTracker.Web.Services.Schedules;

/// <summary>Vstup jednoho kroku do datum-výpočtu. Plán i skutečnost jsou absolutní datumy (NULL = nevyplněno).</summary>
public sealed record ScheduleDateStep(int Poradi, DateTime? PlanDatum, DateTime? SkutecnostDatum);

/// <summary>
/// Výsledek jednoho kroku — plánový segment a segment skutečnosti. Skutečnost se kreslí u vyplněných
/// kroků a u <b>aktuálního kroku</b> (první nevyplněný po posledním vyplněném), který se táhne do dneška.
/// </summary>
public sealed record ScheduleDateStepResult(
    int Poradi,
    DateTime PlanStart,
    DateTime PlanEnd,
    bool MaSkutecnost,
    DateTime SkutecnostStart,
    DateTime SkutecnostEnd,
    HarmonogramKrokStav Stav,
    bool JeAktualniKrok);

/// <summary>Souhrn harmonogramu (datum-model, sjednocený stav). Viz spec harmonogram-plan-vs-skutecnost.</summary>
public sealed record ScheduleDateSummary(
    DateTime PlanoveDokonceni,
    DateTime Termin,
    int? AktualniKrokPoradi,   // null = vše vyplněno (Dokončeno)
    int PrekroceniDni,         // dnes − plán(aktuální krok), znaménkově; 0 když Dokončeno
    bool Dokonceno);

/// <summary>
/// Datum-model výpočtu harmonogramu (žádné offsety). Plán: segment kroku = [plan(k-1), plan(k)],
/// plan(0)=start. Skutečnost: vyplněné kroky [skut(předchozí vyplněný), skut(k)], nevyplněné mezery
/// se pohltí. Aktuální krok (první nevyplněný po posledním vyplněném) se kreslí od konce posledního
/// vyplněného až po dnešek = vizuální prodlení/rozpracováno. Per-krok stav: Čeká / V prodlení / Splněno.
/// </summary>
public static class ScheduleDateCalculator
{
    public static IReadOnlyList<ScheduleDateStepResult> Compute(
        DateTime start, IReadOnlyList<ScheduleDateStep> steps, DateTime today)
    {
        var ordered = steps.OrderBy(x => x.Poradi).ToList();
        var normToday = today.Date;

        // Aktuální krok = první nevyplněný PO posledním vyplněném.
        int? posledniVyplneny = null;
        for (var i = ordered.Count - 1; i >= 0; i--)
            if (ordered[i].SkutecnostDatum.HasValue) { posledniVyplneny = ordered[i].Poradi; break; }

        int? aktualniPoradi = null;
        foreach (var st in ordered)
            if (!st.SkutecnostDatum.HasValue && (posledniVyplneny is null || st.Poradi > posledniVyplneny))
            { aktualniPoradi = st.Poradi; break; }

        var results = new List<ScheduleDateStepResult>(ordered.Count);
        var planCursor = start.Date;
        var skutecnostCursor = start.Date; // konec posledního vyplněného kroku

        foreach (var step in ordered)
        {
            var planStart = planCursor;
            var planEnd = (step.PlanDatum?.Date) ?? planStart;
            if (planEnd < planStart) planEnd = planStart;
            planCursor = planEnd;

            var filled = step.SkutecnostDatum.HasValue;
            var jeAktualni = aktualniPoradi.HasValue && step.Poradi == aktualniPoradi.Value;

            var skutecnostStart = skutecnostCursor;
            var skutecnostEnd = skutecnostCursor;
            var maSkutecnost = false;

            if (filled)
            {
                maSkutecnost = true;
                skutecnostEnd = step.SkutecnostDatum!.Value.Date;
                if (skutecnostEnd < skutecnostStart) skutecnostEnd = skutecnostStart; // nezáporná šířka
                skutecnostCursor = skutecnostEnd;
            }
            else if (jeAktualni)
            {
                // Rozpracovaný krok: táhne se od konce posledního vyplněného do dneška.
                maSkutecnost = true;
                skutecnostEnd = normToday > skutecnostStart ? normToday : skutecnostStart;
                // skutecnostCursor neposouváme — za aktuálním krokem se už nic nekreslí (Čeká).
            }

            var stav = filled
                ? HarmonogramKrokStav.Splneno
                : (normToday > planEnd ? HarmonogramKrokStav.VProdleni : HarmonogramKrokStav.Ceka);

            results.Add(new ScheduleDateStepResult(
                step.Poradi, planStart, planEnd, maSkutecnost, skutecnostStart, skutecnostEnd, stav, jeAktualni));
        }

        return results;
    }

    public static ScheduleDateSummary Summarize(
        DateTime start, IReadOnlyList<ScheduleDateStep> steps, DateTime termin, DateTime today)
    {
        var ordered = steps.OrderBy(x => x.Poradi).ToList();
        var normTermin = termin.Date;
        var computed = Compute(start, ordered, today);

        var planoveDokonceni = computed.Count == 0 ? start.Date : computed[^1].PlanEnd;
        var aktualni = computed.FirstOrDefault(x => x.JeAktualniKrok);

        if (aktualni is null)   // vše vyplněno → Dokončeno
            return new ScheduleDateSummary(planoveDokonceni, normTermin, null, 0, Dokonceno: true);

        var prekroceni = (today.Date - aktualni.PlanEnd.Date).Days;
        return new ScheduleDateSummary(planoveDokonceni, normTermin, aktualni.Poradi, prekroceni, Dokonceno: false);
    }
}
