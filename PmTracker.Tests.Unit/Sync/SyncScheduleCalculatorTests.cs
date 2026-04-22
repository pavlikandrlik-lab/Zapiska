using FluentAssertions;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.Sync;

public sealed class SyncScheduleCalculatorTests
{
    private static DateTimeOffset Utc(int y, int mo, int d, int h = 0, int mi = 0)
        => new(y, mo, d, h, mi, 0, TimeSpan.Zero);

    [Fact]
    public void ComputeNext_NowAfterAnchor_OneTickAhead()
    {
        var anchor = Utc(2026, 1, 1, 0, 0);
        var period = TimeSpan.FromHours(12);
        var now = Utc(2026, 1, 1, 8, 0);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(Utc(2026, 1, 1, 12, 0));
    }

    [Fact]
    public void ComputeNext_NowBeforeAnchor_ReturnsAnchor()
    {
        var anchor = Utc(2026, 1, 1, 16, 0);
        var period = TimeSpan.FromHours(1);
        var now = Utc(2026, 1, 1, 14, 25);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(anchor);
    }

    [Fact]
    public void ComputeNext_NowAfterAnchor_AlignsToAnchorMinutes()
    {
        var anchor = Utc(2026, 1, 1, 14, 38);
        var period = TimeSpan.FromHours(1);
        var now = Utc(2026, 1, 1, 14, 30);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(anchor);
    }

    [Fact]
    public void ComputeNext_NowEqualsAnchor_ReturnsAnchor()
    {
        var anchor = Utc(2026, 1, 1, 12, 0);
        var period = TimeSpan.FromHours(1);

        var next = SyncScheduleCalculator.ComputeNext(anchor, anchor, period);

        next.Should().Be(anchor);
    }

    [Fact]
    public void ComputeNext_ZeroPeriod_ThrowsArgumentOutOfRange()
    {
        var anchor = Utc(2026, 1, 1);
        var now = Utc(2026, 1, 2);

        var act = () => SyncScheduleCalculator.ComputeNext(now, anchor, TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*period*");
    }

    [Fact]
    public void ComputeNext_NegativePeriod_ThrowsArgumentOutOfRange()
    {
        var anchor = Utc(2026, 1, 1);
        var now = Utc(2026, 1, 2);

        var act = () => SyncScheduleCalculator.ComputeNext(now, anchor, TimeSpan.FromHours(-1));

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*period*");
    }

    [Fact]
    public void ComputeNext_CrossesMidnight()
    {
        var anchor = Utc(2026, 1, 1, 23, 0);
        var period = TimeSpan.FromHours(2);
        var now = Utc(2026, 1, 2, 0, 30);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(Utc(2026, 1, 2, 1, 0));
    }

    [Fact]
    public void ComputeNext_DstTransitionDay_UtcImmune()
    {
        var anchor = Utc(2026, 3, 28, 0, 0);
        var period = TimeSpan.FromHours(1);
        var now = Utc(2026, 3, 29, 2, 30);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(Utc(2026, 3, 29, 3, 0));
    }

    [Fact]
    public void ComputeNext_PeriodGreaterThan24h_WorksAcrossDays()
    {
        var anchor = Utc(2026, 1, 1, 0, 0);
        var period = TimeSpan.FromHours(48);
        var now = Utc(2026, 1, 2, 0, 0);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(Utc(2026, 1, 3, 0, 0));
    }

    [Fact]
    public void ComputeNext_VeryLargePeriodSevenDays()
    {
        var anchor = Utc(2026, 1, 1, 0, 0);
        var period = TimeSpan.FromDays(7);
        var now = Utc(2026, 1, 5, 12, 0);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(Utc(2026, 1, 8, 0, 0));
    }
}
