using FluentAssertions;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Datum-model: server je jediný zdroj pozic baru (left%/width%) pro statická zobrazení.
/// Osa je přichycená na celé měsíce (start = 1. dne měsíce začátku, end = 1. den měsíce PO obsahu →
/// pravý okraj je vždy popsaný měsíční předěl); markery „dnes"/„termín" jsou nullable (skryté mimo interval).
/// Bez DB / DOM → čistá funkce.
/// Spec: docs/superpowers/specs/2026-06-23-harmonogram-osa-redesign-design.md
/// </summary>
public sealed class ScheduleBarLayoutCalculatorTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    [Fact]
    public void Compute_TwoSteps_AxisSnapsToWholeMonths()
    {
        // start 1.1., deadline 1.2. → contentEnd = 1.2. → axisEnd = 1.3.2026 (1. den měsíce po obsahu, 59 dní).
        var steps = new[]
        {
            new ScheduleDateStepResult(1, Start, new DateTime(2026, 1, 11), MaSkutecnost: true,  Start, new DateTime(2026, 1, 13), HarmonogramKrokStav.Splneno, JeAktualniKrok: false),
            new ScheduleDateStepResult(2, new DateTime(2026, 1, 11), new DateTime(2026, 1, 21), MaSkutecnost: false, new DateTime(2026, 1, 13), new DateTime(2026, 1, 13), HarmonogramKrokStav.Ceka, JeAktualniKrok: false),
        };

        var layout = ScheduleBarLayoutCalculator.Compute(
            Start, deadline: new DateTime(2026, 2, 1), today: new DateTime(2026, 1, 15), steps);

        layout.AxisStart.Should().Be(new DateTime(2026, 1, 1));
        layout.AxisEnd.Should().Be(new DateTime(2026, 3, 1));
        layout.TotalDays.Should().Be(59);

        layout.TodayPct.Should().NotBeNull();
        layout.TodayPct!.Value.Should().BeApproximately(14 * 100.0 / 59, 0.01);
        layout.DeadlinePct.Should().NotBeNull();
        layout.DeadlinePct!.Value.Should().BeApproximately(31 * 100.0 / 59, 0.01);

        var s1 = layout.Segments.Single(s => s.Poradi == 1);
        s1.PlanLeftPct.Should().BeApproximately(0.0, 0.01);
        s1.PlanWidthPct.Should().BeApproximately(10 * 100.0 / 59, 0.01);
        s1.HasActual.Should().BeTrue();
        s1.ActualLeftPct.Should().BeApproximately(0.0, 0.01);
        s1.ActualWidthPct.Should().BeApproximately(12 * 100.0 / 59, 0.01);

        var s2 = layout.Segments.Single(s => s.Poradi == 2);
        s2.PlanLeftPct.Should().BeApproximately(10 * 100.0 / 59, 0.01);
        s2.PlanWidthPct.Should().BeApproximately(10 * 100.0 / 59, 0.01);
        s2.HasActual.Should().BeFalse();
    }

    [Fact]
    public void Compute_ZeroWidthPlanStep_ProducesZeroWidthSegment()
    {
        // Krok s plan_datum == předchozí konec → nulová šířka (nevykreslí se) — nezávisle na ose.
        var steps = new[]
        {
            new ScheduleDateStepResult(1, Start, new DateTime(2026, 1, 11), MaSkutecnost: false, Start, Start, HarmonogramKrokStav.Ceka, JeAktualniKrok: false),
            new ScheduleDateStepResult(2, new DateTime(2026, 1, 11), new DateTime(2026, 1, 11), MaSkutecnost: false, Start, Start, HarmonogramKrokStav.Ceka, JeAktualniKrok: false),
        };

        var layout = ScheduleBarLayoutCalculator.Compute(
            Start, deadline: new DateTime(2026, 1, 21), today: Start, steps);

        layout.Segments.Single(s => s.Poradi == 2).PlanWidthPct.Should().Be(0.0);
    }

    [Fact]
    public void Compute_AxisEndIsFirstDayOfMonthAfterContent()
    {
        // contentEnd = 31.3. → axisEnd = 1.4. (1. den měsíce po obsahu, ne poslední den měsíce);
        // pravý okraj osy je tak vždy popsaný měsíční předěl a deadline nelícuje s krajem.
        var steps = new[]
        {
            new ScheduleDateStepResult(1, Start, new DateTime(2026, 3, 31), MaSkutecnost: false, Start, Start, HarmonogramKrokStav.Ceka, JeAktualniKrok: false),
        };

        var layout = ScheduleBarLayoutCalculator.Compute(
            Start, deadline: new DateTime(2026, 3, 31), today: new DateTime(2026, 2, 1), steps);

        layout.AxisEnd.Should().Be(new DateTime(2026, 4, 1));
        layout.MonthTicks.Should().Contain(t => t.Label == "04/2026" && t.LeftPct == 100.0,
            "1. den měsíce po obsahu leží přesně na pravém okraji → popsaný");
    }

    [Fact]
    public void Compute_TodayOutsideAxis_TodayPctIsNull()
    {
        // úkol v budoucnu: start a plán v dubnu, dnes v lednu → dnes před osou → marker skrytý.
        var aprilStart = new DateTime(2026, 4, 1);
        var steps = new[]
        {
            new ScheduleDateStepResult(1, aprilStart, new DateTime(2026, 4, 10), MaSkutecnost: false, aprilStart, aprilStart, HarmonogramKrokStav.Ceka, JeAktualniKrok: false),
        };

        var layout = ScheduleBarLayoutCalculator.Compute(
            aprilStart, deadline: new DateTime(2026, 4, 10), today: new DateTime(2026, 1, 15), steps);

        layout.TodayPct.Should().BeNull();
    }

    [Fact]
    public void Compute_MonthTicks_AreFirstDayOfEachMonth()
    {
        // osa 1.1. → 1.3. → ticky 1.1.(0%), 1.2.(31/59) a 1.3.(100% — pravý okraj).
        var steps = new[]
        {
            new ScheduleDateStepResult(1, Start, new DateTime(2026, 1, 11), MaSkutecnost: false, Start, Start, HarmonogramKrokStav.Ceka, JeAktualniKrok: false),
        };

        var layout = ScheduleBarLayoutCalculator.Compute(
            Start, deadline: new DateTime(2026, 2, 1), today: new DateTime(2026, 1, 15), steps);

        layout.MonthTicks.Should().HaveCount(3);
        layout.MonthTicks[0].LeftPct.Should().BeApproximately(0.0, 0.01);
        layout.MonthTicks[0].Label.Should().Be("01/2026");
        layout.MonthTicks[1].Label.Should().Be("02/2026");
        layout.MonthTicks[1].LeftPct.Should().BeApproximately(31 * 100.0 / 59, 0.01);
        layout.MonthTicks[2].Label.Should().Be("03/2026");
        layout.MonthTicks[2].LeftPct.Should().BeApproximately(100.0, 0.01);
    }
}
