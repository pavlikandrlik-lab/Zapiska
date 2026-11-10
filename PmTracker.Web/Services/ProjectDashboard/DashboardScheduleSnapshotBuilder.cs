using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services.ProjectDashboard;

/// <summary>
/// Kroky harmonogramu záznamu převedené na vstup <see cref="DashboardRecordCategorizer"/>.
/// </summary>
public static class DashboardScheduleSnapshotBuilder
{
    public static IReadOnlyList<ScheduleStepSnapshot> Build(
        DateTime start, IReadOnlyList<ScheduleDateStep> steps, DateTime referenceDate)
    {
        var computed = ScheduleDateCalculator.Compute(start, steps, referenceDate);
        // Hodnotí se jen kroky s vlastním plánem. U kroku bez plánu je konec plánu dopočtený
        // (konec předchozího plánu nebo datum založení) a zpoždění ani „plán vypršel“ k němu nedávají
        // smysl — stejné pravidlo jako stav harmonogramu (spec 2026-10-24 R2).
        var poradiSPlanem = steps.Where(s => s.PlanDatum.HasValue).Select(s => s.Poradi).ToHashSet();

        return computed
            .Where(c => poradiSPlanem.Contains(c.Poradi))
            .Select(c =>
            {
                // Dashboard kategorizace pracuje jen se SKUTEČNĚ vyplněnou skutečností; projekci
                // aktuálního kroku do dneška (vizuální prvek lišty) sem nepouštíme (JeAktualniKrok guard).
                var maRealnouSkutecnost = c.MaSkutecnost && !c.JeAktualniKrok;
                var nazev = HarmonogramKroky.Vse.First(d => d.Poradi == c.Poradi).Nazev;
                var actualEnd = maRealnouSkutecnost ? c.SkutecnostEnd : c.PlanEnd;
                var duration = (c.PlanEnd - c.PlanStart).Days;
                var offset = maRealnouSkutecnost ? (c.SkutecnostEnd - c.PlanEnd).Days : 0;
                return new ScheduleStepSnapshot(c.Poradi, nazev, c.PlanEnd, actualEnd, duration, offset, maRealnouSkutecnost);
            })
            .ToList();
    }
}
