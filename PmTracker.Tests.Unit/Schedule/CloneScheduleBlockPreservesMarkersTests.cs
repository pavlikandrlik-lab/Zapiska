using System;
using System.Collections.Generic;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Regrese: detail návrhu staví HarmonogramBlockViewModel přes CloneScheduleBlock. Dřív object-initializer
/// tiše zahazoval OverviewLayout (→ null) a Today (→ default 0001-01-01); marker „Dnes"/„Termín" tak přišel
/// o inline pozici a CSS ho přilepil na left:0 (fixní relikt). Clone musí marker-řídící pole zachovat.
/// </summary>
public sealed class CloneScheduleBlockPreservesMarkersTests
{
    private static HarmonogramBlockViewModel SourceWithMarkers()
    {
        var layout = new ScheduleBarLayout(
            AxisStart: new DateTime(2026, 1, 1),
            AxisEnd: new DateTime(2026, 7, 31),
            TotalDays: 211,
            TodayPct: 42.5,
            DeadlinePct: 90.0,
            Segments: new List<ScheduleBarSegment>(),
            MonthTicks: new List<ScheduleMonthTick>());

        return new HarmonogramBlockViewModel
        {
            RecordId = 7,
            Mode = "project-readonly",
            DatumZalozeni = new DateTime(2026, 1, 15),
            TerminUkonceni = new DateTime(2026, 7, 1),
            Today = new DateTime(2026, 6, 25),
            OverviewLayout = layout,
        };
    }

    [Fact]
    public void CloneScheduleBlock_preserves_OverviewLayout_and_Today()
    {
        var source = SourceWithMarkers();

        var clone = RecordProposalService.CloneScheduleBlock(source);

        clone.OverviewLayout.Should().BeSameAs(source.OverviewLayout,
            "marker pozice (TodayPct/DeadlinePct) jsou ve view jediný zdroj pravdy — bez nich se marker přilepí na left:0");
        clone.Today.Should().Be(new DateTime(2026, 6, 25),
            "Today nesmí spadnout na default 0001-01-01");
    }

    [Fact]
    public void CloneScheduleBlock_override_termin_still_keeps_markers()
    {
        var source = SourceWithMarkers();

        var clone = RecordProposalService.CloneScheduleBlock(source, terminUkonceni: new DateTime(2026, 8, 10));

        clone.TerminUkonceni.Should().Be(new DateTime(2026, 8, 10));
        clone.OverviewLayout.Should().BeSameAs(source.OverviewLayout);
        clone.Today.Should().Be(source.Today);
    }
}
