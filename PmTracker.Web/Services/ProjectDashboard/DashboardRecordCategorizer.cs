using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.ProjectDashboard;

public sealed record ScheduleStepSnapshot(
    int StepIndex,
    string Name,
    DateTime PlanEndDate,
    DateTime ActualEndDate,
    int DurationDays,
    int OffsetDays);

public sealed record RecordCategorizationResult(
    DashboardRecordCategory Category,
    int WorstOffsetDays,
    IReadOnlyList<(ScheduleStepSnapshot Step, DashboardRecordCategory StepCategory)> ProblematicSteps);

public static class DashboardRecordCategorizer
{
    public const int DefaultApproachingThresholdDays = 7;

    /// <summary>
    /// Categorizes a single schedule step.
    /// - Delayed: actual is behind plan (OffsetDays > 0)
    /// - AwaitingActual: plan date has passed but actual equals plan (no real actual recorded yet, offset is 0)
    /// - ApproachingDeadline: plan date is within threshold days in the future, no actual yet (offset is 0)
    /// Returns null if step is on track.
    /// </summary>
    public static DashboardRecordCategory? CategorizeStep(ScheduleStepSnapshot step, DateTime referenceDate, int approachingThresholdDays)
    {
        // Step has positive offset — actual is behind plan
        if (step.OffsetDays > 0)
        {
            return DashboardRecordCategory.Delayed;
        }

        // Plan date is in the past, offset is 0, actual <= plan — waiting for actual data
        if (step.PlanEndDate < referenceDate && step.OffsetDays == 0 && step.ActualEndDate <= step.PlanEndDate)
        {
            return DashboardRecordCategory.AwaitingActual;
        }

        // Plan date is in the near future, offset is 0 — approaching deadline
        var daysUntilPlan = (step.PlanEndDate - referenceDate).Days;
        if (daysUntilPlan >= 0 && daysUntilPlan <= approachingThresholdDays && step.OffsetDays == 0)
        {
            return DashboardRecordCategory.ApproachingDeadline;
        }

        return null;
    }

    /// <summary>
    /// Categorizes an entire record by examining all its schedule steps.
    /// Returns the worst category and worst offset across all problematic steps.
    /// </summary>
    public static RecordCategorizationResult CategorizeRecord(
        IReadOnlyList<ScheduleStepSnapshot> steps,
        DateTime referenceDate,
        int approachingThresholdDays)
    {
        var problematic = new List<(ScheduleStepSnapshot Step, DashboardRecordCategory StepCategory)>();
        DashboardRecordCategory? worstCategory = null;
        var worstOffset = 0;

        foreach (var step in steps)
        {
            var cat = CategorizeStep(step, referenceDate, approachingThresholdDays);
            if (cat is null)
            {
                continue;
            }

            problematic.Add((step, cat.Value));

            var effectiveOffset = cat.Value switch
            {
                DashboardRecordCategory.Delayed => step.OffsetDays,
                DashboardRecordCategory.AwaitingActual => (referenceDate - step.PlanEndDate).Days,
                DashboardRecordCategory.ApproachingDeadline => (step.PlanEndDate - referenceDate).Days,
                _ => 0
            };

            if (worstCategory is null
                || CategorySeverity(cat.Value) < CategorySeverity(worstCategory.Value)
                || (cat.Value == worstCategory.Value && effectiveOffset > worstOffset))
            {
                worstCategory = cat.Value;
                worstOffset = effectiveOffset;
            }
        }

        return new RecordCategorizationResult(
            worstCategory ?? DashboardRecordCategory.ApproachingDeadline,
            worstOffset,
            problematic);
    }

    /// <summary>
    /// Sort key for ordering records — lower severity value = more severe, then by descending offset.
    /// </summary>
    public static (int Severity, int NegatedOffset) SortKey((DashboardRecordCategory Category, int WorstOffset) record)
    {
        return (CategorySeverity(record.Category), -record.WorstOffset);
    }

    private static int CategorySeverity(DashboardRecordCategory category) => category switch
    {
        DashboardRecordCategory.Delayed => 0,
        DashboardRecordCategory.AwaitingActual => 1,
        DashboardRecordCategory.ApproachingDeadline => 2,
        _ => 3
    };
}
