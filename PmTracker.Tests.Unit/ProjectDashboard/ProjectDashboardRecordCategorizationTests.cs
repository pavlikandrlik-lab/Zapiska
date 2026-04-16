using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;

namespace PmTracker.Tests.Unit.ProjectDashboard;

public sealed class ProjectDashboardRecordCategorizationTests
{
    private static readonly DateTime ReferenceDate = new(2026, 4, 16);

    [Fact]
    public void ShouldCategorize_AsDelayed_WhenStepHasPositiveOffset()
    {
        var step = new ScheduleStepSnapshot(
            StepIndex: 1, Name: "Dodávka",
            PlanEndDate: new DateTime(2026, 4, 1),
            ActualEndDate: new DateTime(2026, 4, 8),
            DurationDays: 5, OffsetDays: 7);

        var result = DashboardRecordCategorizer.CategorizeStep(step, ReferenceDate, approachingThresholdDays: 7);

        result.Should().Be(DashboardRecordCategory.Delayed);
    }

    [Fact]
    public void ShouldCategorize_AsAwaitingActual_WhenPlanExpiredAndActualIsZero()
    {
        var step = new ScheduleStepSnapshot(
            StepIndex: 1, Name: "Dodávka",
            PlanEndDate: new DateTime(2026, 4, 1),
            ActualEndDate: new DateTime(2026, 4, 1),
            DurationDays: 5, OffsetDays: 0);

        var result = DashboardRecordCategorizer.CategorizeStep(step, ReferenceDate, approachingThresholdDays: 7);

        result.Should().Be(DashboardRecordCategory.AwaitingActual);
    }

    [Fact]
    public void ShouldCategorize_AsApproaching_WhenPlanWithinThreshold()
    {
        var step = new ScheduleStepSnapshot(
            StepIndex: 1, Name: "Dodávka",
            PlanEndDate: new DateTime(2026, 4, 20),
            ActualEndDate: new DateTime(2026, 4, 20),
            DurationDays: 5, OffsetDays: 0);

        var result = DashboardRecordCategorizer.CategorizeStep(step, ReferenceDate, approachingThresholdDays: 7);

        result.Should().Be(DashboardRecordCategory.ApproachingDeadline);
    }

    [Fact]
    public void ShouldReturnNull_WhenStepIsOnTrack()
    {
        var step = new ScheduleStepSnapshot(
            StepIndex: 1, Name: "Dodávka",
            PlanEndDate: new DateTime(2026, 5, 1),
            ActualEndDate: new DateTime(2026, 5, 1),
            DurationDays: 5, OffsetDays: 0);

        var result = DashboardRecordCategorizer.CategorizeStep(step, ReferenceDate, approachingThresholdDays: 7);

        result.Should().BeNull();
    }

    [Fact]
    public void ShouldPickWorstCategory_WhenMultipleStepsAreProblematic()
    {
        var steps = new[]
        {
            new ScheduleStepSnapshot(1, "Krok1", new DateTime(2026, 4, 20), new DateTime(2026, 4, 20), 5, 0),
            new ScheduleStepSnapshot(2, "Krok2", new DateTime(2026, 4, 1), new DateTime(2026, 4, 8), 5, 7)
        };

        var result = DashboardRecordCategorizer.CategorizeRecord(steps, ReferenceDate, approachingThresholdDays: 7);

        result.Category.Should().Be(DashboardRecordCategory.Delayed);
        result.WorstOffsetDays.Should().Be(7);
        result.ProblematicSteps.Should().HaveCount(2);
    }

    [Fact]
    public void ShouldSortRecords_ByWorstOffsetDescending()
    {
        var records = new List<(DashboardRecordCategory Category, int WorstOffset)>
        {
            (DashboardRecordCategory.ApproachingDeadline, 3),
            (DashboardRecordCategory.Delayed, 15),
            (DashboardRecordCategory.AwaitingActual, 10),
            (DashboardRecordCategory.Delayed, 5)
        };

        var sorted = records.OrderBy(DashboardRecordCategorizer.SortKey).ToList();

        sorted[0].WorstOffset.Should().Be(15);
        sorted[1].WorstOffset.Should().Be(5);
        sorted[2].WorstOffset.Should().Be(10);
        sorted[3].WorstOffset.Should().Be(3);
    }

    [Fact]
    public void ShouldReturnEmptyProblematicSteps_WhenAllStepsOnTrack()
    {
        var steps = new[]
        {
            new ScheduleStepSnapshot(1, "Krok1", new DateTime(2026, 5, 1), new DateTime(2026, 5, 1), 5, 0),
            new ScheduleStepSnapshot(2, "Krok2", new DateTime(2026, 6, 1), new DateTime(2026, 6, 1), 5, 0)
        };

        var result = DashboardRecordCategorizer.CategorizeRecord(steps, ReferenceDate, approachingThresholdDays: 7);

        result.ProblematicSteps.Should().BeEmpty();
    }
}
