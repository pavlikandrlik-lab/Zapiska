namespace PmTracker.Web.Services.Schedules;

/// <summary>Pozice jednoho kroku v baru (procenta osy).</summary>
public sealed record ScheduleBarSegment(
    int Poradi,
    double PlanLeftPct,
    double PlanWidthPct,
    bool HasActual,
    double ActualLeftPct,
    double ActualWidthPct);

/// <summary>Měsíční tick osy — pozice (%) a popisek „MM/RRRR".</summary>
public sealed record ScheduleMonthTick(double LeftPct, string Label);

/// <summary>
/// Kompletní rozložení baru — osa přichycená na celé měsíce + markery + segmenty kroků + měsíční ticky.
/// <see cref="TodayPct"/> a <see cref="DeadlinePct"/> jsou null, pokud datum leží mimo interval osy
/// (úkol v budoucnu / proběhlý) → marker se nevykreslí.
/// </summary>
public sealed record ScheduleBarLayout(
    DateTime AxisStart,
    DateTime AxisEnd,
    int TotalDays,
    double? TodayPct,
    double? DeadlinePct,
    IReadOnlyList<ScheduleBarSegment> Segments,
    IReadOnlyList<ScheduleMonthTick> MonthTicks);

/// <summary>
/// Datum-model: server-side pozicování baru. Jediný zdroj pravdy pro statická zobrazení (karta, tab).
/// Osa je přichycená na celé měsíce: start = 1. dne měsíce začátku, end = 1. den měsíce PO obsahu
/// (obsah = max z termínu, konců plánu a skutečnosti). Pravý okraj je tak vždy popsaný měsíční předěl
/// (tick na 100 %) a rozhodující datum (deadline/„dnes") nelícuje s krajem. Mirror v JS: scheduleAxis.js
/// (parita přes golden-vector fixtures). Spec: 2026-06-23-harmonogram-osa-redesign-design.md.
/// </summary>
public static class ScheduleBarLayoutCalculator
{
    private static DateTime FirstDayOfMonth(DateTime d) => new(d.Year, d.Month, 1);

    public static ScheduleBarLayout Compute(
        DateTime start, DateTime deadline, DateTime today, IReadOnlyList<ScheduleDateStepResult> steps)
    {
        var contentEnd = deadline.Date;
        foreach (var step in steps)
        {
            if (step.PlanEnd.Date > contentEnd) contentEnd = step.PlanEnd.Date;
            if (step.MaSkutecnost && step.SkutecnostEnd.Date > contentEnd) contentEnd = step.SkutecnostEnd.Date;
        }

        var axisStart = FirstDayOfMonth(start.Date);
        // axisEnd = 1. den měsíce PO obsahu → pravý okraj osy je vždy popsaný měsíční předěl (tick na 100 %)
        // a rozhodující datum (deadline/dnes) nelícuje s krajem. Nahrazuje dřívější „poslední den měsíce
        // (+1 měsíc, je-li poslední den)“, které vlevo nechávalo pravý okraj bez popisku.
        var axisEnd = FirstDayOfMonth(contentEnd).AddMonths(1);
        var totalDays = Math.Max(1, (axisEnd - axisStart).Days);

        double Pct(DateTime d) => Math.Clamp((d.Date - axisStart).Days * 100.0 / totalDays, 0.0, 100.0);
        double Width(DateTime from, DateTime to) =>
            Math.Clamp(Math.Max(0, (to.Date - from.Date).Days) * 100.0 / totalDays, 0.0, 100.0);
        bool InAxis(DateTime d) => d.Date >= axisStart && d.Date <= axisEnd;

        var segments = steps
            .OrderBy(x => x.Poradi)
            .Select(x => new ScheduleBarSegment(
                x.Poradi,
                Pct(x.PlanStart), Width(x.PlanStart, x.PlanEnd),
                x.MaSkutecnost,
                x.MaSkutecnost ? Pct(x.SkutecnostStart) : 0.0,
                x.MaSkutecnost ? Width(x.SkutecnostStart, x.SkutecnostEnd) : 0.0))
            .ToList();

        var ticks = new List<ScheduleMonthTick>();
        for (var m = axisStart; m <= axisEnd; m = m.AddMonths(1))
            ticks.Add(new ScheduleMonthTick((m - axisStart).Days * 100.0 / totalDays, $"{m.Month:00}/{m.Year}"));

        return new ScheduleBarLayout(
            axisStart, axisEnd, totalDays,
            InAxis(today) ? Pct(today) : null,
            InAxis(deadline) ? Pct(deadline) : null,
            segments, ticks);
    }
}
