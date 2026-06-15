namespace PmTracker.Web.Services.Schedules;

/// <summary>Pozice jednoho kroku v baru (procenta osy).</summary>
public sealed record ScheduleBarSegment(
    int Poradi,
    double PlanLeftPct,
    double PlanWidthPct,
    bool HasActual,
    double ActualLeftPct,
    double ActualWidthPct);

/// <summary>Kompletní rozložení baru — osa + markery + segmenty kroků.</summary>
public sealed record ScheduleBarLayout(
    DateTime AxisStart,
    DateTime AxisEnd,
    int TotalDays,
    double TodayPct,
    double DeadlinePct,
    IReadOnlyList<ScheduleBarSegment> Segments);

/// <summary>
/// Datum-model (Fáze 3b): server-side pozicování baru. Jediný zdroj pravdy pro statická
/// zobrazení (karta, tab) — klient (block.js) je v statickém režimu nepřepočítává. Mirror
/// této matematiky běží v block.js jen pro editor live-preview. Čistá funkce nad výsledkem
/// <see cref="ScheduleDateCalculator"/>.
/// </summary>
public static class ScheduleBarLayoutCalculator
{
    public static ScheduleBarLayout Compute(
        DateTime start,
        DateTime deadline,
        DateTime today,
        IReadOnlyList<ScheduleDateStepResult> steps)
    {
        var axisStart = start.Date;
        var axisEnd = axisStart;
        void Extend(DateTime d) { if (d.Date > axisEnd) axisEnd = d.Date; }
        Extend(deadline);
        Extend(today);
        foreach (var s in steps)
        {
            Extend(s.PlanEnd);
            if (s.MaSkutecnost) Extend(s.SkutecnostEnd);
        }

        var totalDays = Math.Max(1, (axisEnd - axisStart).Days);

        double Pct(DateTime d)
        {
            var days = (d.Date - axisStart).Days;
            return Math.Clamp(days * 100.0 / totalDays, 0.0, 100.0);
        }

        double Width(DateTime from, DateTime to)
        {
            var days = Math.Max(0, (to.Date - from.Date).Days);
            return Math.Clamp(days * 100.0 / totalDays, 0.0, 100.0);
        }

        var segments = steps
            .OrderBy(s => s.Poradi)
            .Select(s => new ScheduleBarSegment(
                s.Poradi,
                PlanLeftPct: Pct(s.PlanStart),
                PlanWidthPct: Width(s.PlanStart, s.PlanEnd),
                HasActual: s.MaSkutecnost,
                ActualLeftPct: s.MaSkutecnost ? Pct(s.SkutecnostStart) : 0.0,
                ActualWidthPct: s.MaSkutecnost ? Width(s.SkutecnostStart, s.SkutecnostEnd) : 0.0))
            .ToList();

        return new ScheduleBarLayout(
            axisStart, axisEnd, totalDays, Pct(today), Pct(deadline), segments);
    }
}
