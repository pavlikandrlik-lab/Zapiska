using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Datum-model (Fáze 3b): HarmonogramDateBlokBuilder.BuildBarLayout zapojuje krok rows
/// → ScheduleDateCalculator → ScheduleBarLayoutCalculator (server-side pozice baru).
/// </summary>
public sealed class HarmonogramDateBlokBuilderBarLayoutTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    private static ZaznamHarmonogramKrokEntity Krok(int poradi, DateTime? plan, DateTime? skut) =>
        new() { ZaznamId = 1, Poradi = (byte)poradi, PlanDatum = plan, SkutecnostDatum = skut };

    [Fact]
    public void BuildBarLayout_PositionsPlanAndActualFromRows()
    {
        var rows = new[]
        {
            Krok(1, new DateTime(2026, 1, 11), new DateTime(2026, 1, 13)),
            Krok(2, new DateTime(2026, 1, 21), null),
        };

        var layout = HarmonogramDateBlokBuilder.BuildBarLayout(
            Start, rows, termin: new DateTime(2026, 2, 1), today: new DateTime(2026, 1, 15));

        layout.TotalDays.Should().Be(31);
        var s1 = layout.Segments.Single(s => s.Poradi == 1);
        s1.PlanWidthPct.Should().BeApproximately(10 * 100.0 / 31, 0.01);
        s1.HasActual.Should().BeTrue();
        s1.ActualWidthPct.Should().BeApproximately(12 * 100.0 / 31, 0.01);

        // Kroky 3–10 bez dat (NULL) → nulová šířka plánu (plan_datum NULL → planEnd=planStart).
        layout.Segments.Single(s => s.Poradi == 10).PlanWidthPct.Should().Be(0.0);
    }
}
