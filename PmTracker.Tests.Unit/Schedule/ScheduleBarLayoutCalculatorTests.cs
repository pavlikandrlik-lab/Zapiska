using FluentAssertions;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Datum-model (Fáze 3b): server je jediný zdroj pozic baru (left%/width%) pro statická
/// zobrazení. Tento kalkulátor portuje pozicování z klientského block.js na server
/// nad výsledkem <see cref="ScheduleDateCalculator"/>. Bez DB / DOM → čistá funkce.
/// </summary>
public sealed class ScheduleBarLayoutCalculatorTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    [Fact]
    public void Compute_TwoSteps_PositionsPlanAndActualSegmentsAsPercentOfAxis()
    {
        // Plán: krok1 → 11.1. (10 dní), krok2 → 21.1. (10 dní). Skutečnost: krok1 13.1., krok2 nevyplněn.
        var steps = new[]
        {
            new ScheduleDateStepResult(1, Start, new DateTime(2026, 1, 11), MaSkutecnost: true,  Start, new DateTime(2026, 1, 13)),
            new ScheduleDateStepResult(2, new DateTime(2026, 1, 11), new DateTime(2026, 1, 21), MaSkutecnost: false, new DateTime(2026, 1, 13), new DateTime(2026, 1, 13)),
        };

        var layout = ScheduleBarLayoutCalculator.Compute(
            Start,
            deadline: new DateTime(2026, 2, 1),
            today: new DateTime(2026, 1, 15),
            steps);

        // Osa: start 1.1. → max(deadline 1.2., today, planEnd, actualEnd) = 1.2. → 31 dní.
        layout.TotalDays.Should().Be(31);
        layout.AxisEnd.Should().Be(new DateTime(2026, 2, 1));

        // today = 14. den, deadline = 31. den.
        layout.TodayPct.Should().BeApproximately(14 * 100.0 / 31, 0.01);
        layout.DeadlinePct.Should().BeApproximately(100.0, 0.01);

        var s1 = layout.Segments.Single(s => s.Poradi == 1);
        s1.PlanLeftPct.Should().BeApproximately(0.0, 0.01);
        s1.PlanWidthPct.Should().BeApproximately(10 * 100.0 / 31, 0.01);
        s1.HasActual.Should().BeTrue();
        s1.ActualLeftPct.Should().BeApproximately(0.0, 0.01);
        s1.ActualWidthPct.Should().BeApproximately(12 * 100.0 / 31, 0.01);

        var s2 = layout.Segments.Single(s => s.Poradi == 2);
        s2.PlanLeftPct.Should().BeApproximately(10 * 100.0 / 31, 0.01);
        s2.PlanWidthPct.Should().BeApproximately(10 * 100.0 / 31, 0.01);
        s2.HasActual.Should().BeFalse();
    }

    [Fact]
    public void Compute_ZeroWidthPlanStep_ProducesZeroWidthSegment()
    {
        // Krok s plan_datum == předchozí konec → nulová šířka (nevykreslí se).
        var steps = new[]
        {
            new ScheduleDateStepResult(1, Start, new DateTime(2026, 1, 11), MaSkutecnost: false, Start, Start),
            new ScheduleDateStepResult(2, new DateTime(2026, 1, 11), new DateTime(2026, 1, 11), MaSkutecnost: false, Start, Start),
        };

        var layout = ScheduleBarLayoutCalculator.Compute(
            Start, deadline: new DateTime(2026, 1, 21), today: Start, steps);

        layout.Segments.Single(s => s.Poradi == 2).PlanWidthPct.Should().Be(0.0);
    }

    [Fact]
    public void Compute_AxisExtendsToTodayWhenLatest()
    {
        // Deadline i plán dřív než dnešek → osa musí sahat k dnešku (jinak marker mimo).
        var steps = new[]
        {
            new ScheduleDateStepResult(1, Start, new DateTime(2026, 1, 6), MaSkutecnost: false, Start, Start),
        };

        var layout = ScheduleBarLayoutCalculator.Compute(
            Start, deadline: new DateTime(2026, 1, 6), today: new DateTime(2026, 1, 21), steps);

        layout.AxisEnd.Should().Be(new DateTime(2026, 1, 21));
        layout.TotalDays.Should().Be(20);
        layout.TodayPct.Should().BeApproximately(100.0, 0.01);
    }
}
