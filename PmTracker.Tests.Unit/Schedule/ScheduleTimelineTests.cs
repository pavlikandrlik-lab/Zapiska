using FluentAssertions;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class ScheduleTimelineTests
{
    [Fact]
    public void Compute_ShouldSupportSignedActualCompletionAndShiftFollowingStep()
    {
        var startDate = new DateTime(2026, 3, 1);

        var result = ScheduleTimelineCalculator.Compute(
            startDate,
            [
                new ScheduleTimelineStepDefinition
                {
                    StepIndex = 1,
                    Code = "STEP01_DURATION",
                    Name = "První krok",
                    ColorHex = "#111111",
                    DurationTypeId = 11,
                    OffsetTypeId = 12
                },
                new ScheduleTimelineStepDefinition
                {
                    StepIndex = 2,
                    Code = "STEP02_DURATION",
                    Name = "Druhý krok",
                    ColorHex = "#222222",
                    DurationTypeId = 21,
                    OffsetTypeId = 22
                }
            ],
            new Dictionary<int, int>
            {
                [11] = 5,
                [12] = -2,
                [21] = 4,
                [22] = 1
            });

        result.Steps.Should().HaveCount(2);
        result.Steps[0].PlanEndDate.Should().Be(new DateTime(2026, 3, 6));
        result.Steps[0].ActualEndDate.Should().Be(new DateTime(2026, 3, 4));
        result.Steps[1].ActualStartDate.Should().Be(new DateTime(2026, 3, 4));
        result.Steps[1].ActualEndDate.Should().Be(new DateTime(2026, 3, 9));
    }

    [Fact]
    public void Compute_ShouldClampNegativeActualDurationToZero()
    {
        var startDate = new DateTime(2026, 3, 1);

        var result = ScheduleTimelineCalculator.Compute(
            startDate,
            [
                new ScheduleTimelineStepDefinition
                {
                    StepIndex = 1,
                    Code = "STEP01_DURATION",
                    Name = "První krok",
                    ColorHex = "#111111",
                    DurationTypeId = 11,
                    OffsetTypeId = 12
                }
            ],
            new Dictionary<int, int>
            {
                [11] = 3,
                [12] = -10
            });

        result.Steps.Should().ContainSingle();
        result.Steps[0].OffsetDays.Should().Be(-3);
        (result.Steps[0].ActualEndDate - result.Steps[0].ActualStartDate).Days.Should().Be(0);
        result.Steps[0].ActualEndDate.Should().Be(startDate);
    }

    [Fact]
    public void Summarize_ShouldKeepSignedTotalOffset()
    {
        var startDate = new DateTime(2026, 3, 1);
        var steps = ScheduleTimelineCalculator.Compute(
            startDate,
            [
                new ScheduleTimelineStepDefinition
                {
                    StepIndex = 1,
                    Code = "STEP01_DURATION",
                    Name = "První krok",
                    ColorHex = "#111111",
                    DurationTypeId = 11,
                    OffsetTypeId = 12
                },
                new ScheduleTimelineStepDefinition
                {
                    StepIndex = 2,
                    Code = "STEP02_DURATION",
                    Name = "Druhý krok",
                    ColorHex = "#222222",
                    DurationTypeId = 21,
                    OffsetTypeId = 22
                }
            ],
            new Dictionary<int, int>
            {
                [11] = 4,
                [12] = 2,
                [21] = 6,
                [22] = -1
            });

        var summary = ScheduleTimelineCalculator.Summarize(steps, deadline: new DateTime(2026, 3, 12));

        summary.TotalDurationDays.Should().Be(10);
        summary.TotalOffsetDays.Should().Be(1);
        summary.ActualCompletion.Should().Be(new DateTime(2026, 3, 12));
    }
}
