namespace PmTracker.Web.Services.Schedules;

public static class ScheduleTimelineCalculator
{
    public static ScheduleTimelineComputation Compute(
        DateTime startDate,
        IReadOnlyList<ScheduleTimelineStepDefinition> steps,
        IReadOnlyDictionary<int, int>? values)
    {
        var normalizedStart = startDate.Date;
        var normalizedValues = values ?? new Dictionary<int, int>();
        var results = new List<ScheduleTimelineStepResult>(steps.Count);
        var planCursor = normalizedStart;
        var actualCursor = normalizedStart;

        foreach (var step in steps.OrderBy(x => x.StepIndex))
        {
            var durationDays = step.DurationTypeId > 0
                ? Math.Max(0, normalizedValues.GetValueOrDefault(step.DurationTypeId))
                : 0;
            var offsetDays = step.OffsetTypeId > 0
                ? normalizedValues.GetValueOrDefault(step.OffsetTypeId)
                : 0;
            var minOffsetDays = -durationDays;
            if (offsetDays < minOffsetDays)
            {
                offsetDays = minOffsetDays;
            }

            var planStart = planCursor;
            var actualStart = actualCursor;
            var planEnd = planCursor.AddDays(durationDays);
            var actualEnd = actualCursor.AddDays(Math.Max(0, durationDays + offsetDays));

            results.Add(new ScheduleTimelineStepResult
            {
                StepIndex = step.StepIndex,
                Code = step.Code,
                Name = step.Name,
                ColorHex = step.ColorHex,
                DurationTypeId = step.DurationTypeId,
                OffsetTypeId = step.OffsetTypeId,
                DurationDays = durationDays,
                OffsetDays = offsetDays,
                PlanStartDate = planStart,
                PlanEndDate = planEnd,
                ActualStartDate = actualStart,
                ActualEndDate = actualEnd
            });

            planCursor = planEnd;
            actualCursor = actualEnd;
        }

        return new ScheduleTimelineComputation
        {
            Steps = results,
            TotalDurationDays = results.Sum(x => x.DurationDays),
            TotalOffsetDays = results.Sum(x => x.OffsetDays)
        };
    }

    public static ScheduleTimelineSummary Summarize(ScheduleTimelineComputation computation, DateTime deadline)
    {
        var normalizedDeadline = deadline.Date;
        var baselineEnd = computation.Steps.Count == 0
            ? normalizedDeadline
            : computation.Steps[^1].PlanEndDate.Date;
        var actualEnd = computation.Steps.Count == 0
            ? normalizedDeadline
            : computation.Steps[^1].ActualEndDate.Date;
        var isOnTrack = actualEnd <= normalizedDeadline;

        return new ScheduleTimelineSummary
        {
            BaselineCompletion = baselineEnd,
            ActualCompletion = actualEnd,
            Deadline = normalizedDeadline,
            TotalDurationDays = computation.TotalDurationDays,
            TotalOffsetDays = computation.TotalOffsetDays,
            IsOnTrack = isOnTrack,
            OverrunDays = isOnTrack ? 0 : (actualEnd - normalizedDeadline).Days
        };
    }
}

public sealed class ScheduleTimelineStepDefinition
{
    public int StepIndex { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string ColorHex { get; init; }
    public int DurationTypeId { get; init; }
    public int OffsetTypeId { get; init; }
}

public sealed class ScheduleTimelineStepResult
{
    public int StepIndex { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string ColorHex { get; init; }
    public int DurationTypeId { get; init; }
    public int OffsetTypeId { get; init; }
    public int DurationDays { get; init; }
    public int OffsetDays { get; init; }
    public DateTime PlanStartDate { get; init; }
    public DateTime PlanEndDate { get; init; }
    public DateTime ActualStartDate { get; init; }
    public DateTime ActualEndDate { get; init; }
}

public sealed class ScheduleTimelineComputation
{
    public IReadOnlyList<ScheduleTimelineStepResult> Steps { get; init; } = Array.Empty<ScheduleTimelineStepResult>();
    public int TotalDurationDays { get; init; }
    public int TotalOffsetDays { get; init; }
}

public sealed class ScheduleTimelineSummary
{
    public DateTime BaselineCompletion { get; init; }
    public DateTime ActualCompletion { get; init; }
    public DateTime Deadline { get; init; }
    public int TotalDurationDays { get; init; }
    public int TotalOffsetDays { get; init; }
    public bool IsOnTrack { get; init; }
    public int OverrunDays { get; init; }
}
