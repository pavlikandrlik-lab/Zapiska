namespace PmTracker.Web.Services.Schedules;

public sealed class SchedulePreviewRequest
{
    public int RecordId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime DeadlineDate { get; init; }
    public IReadOnlyList<SchedulePreviewStepInput> Steps { get; init; } = Array.Empty<SchedulePreviewStepInput>();
}

public sealed class SchedulePreviewStepInput
{
    public int StepIndex { get; init; }
    public int DurationTypeId { get; init; }
    public int DelayTypeId { get; init; }
    public int DurationDays { get; init; }
    public int DelayDays { get; init; }
}

public sealed class SchedulePreviewResponse
{
    public IReadOnlyList<SchedulePreviewStepResult> Steps { get; init; } = Array.Empty<SchedulePreviewStepResult>();
    public SchedulePreviewSummary Summary { get; init; } = new();
}

public sealed class SchedulePreviewStepResult
{
    public int StepIndex { get; init; }
    public string PlanStart { get; init; } = string.Empty;   // "yyyy-MM-dd"
    public string PlanEnd { get; init; } = string.Empty;
    public string ActualStart { get; init; } = string.Empty;
    public string ActualEnd { get; init; } = string.Empty;
    public int DurationDays { get; init; }
    public int OffsetDays { get; init; }
}

public sealed class SchedulePreviewSummary
{
    public string BaselineCompletion { get; init; } = string.Empty;
    public string ActualCompletion { get; init; } = string.Empty;
    public int TotalDurationDays { get; init; }
    public int TotalOffsetDays { get; init; }
    public bool IsOnTrack { get; init; }
    public int OverrunDays { get; init; }
}

public sealed class SchedulePreviewService
{
    public SchedulePreviewResponse Compute(SchedulePreviewRequest request)
    {
        var stepDefs = request.Steps
            .Select(s => new ScheduleTimelineStepDefinition
            {
                StepIndex = s.StepIndex,
                Code = s.StepIndex.ToString(),
                Name = s.StepIndex.ToString(),
                ColorHex = "#000",
                DurationTypeId = s.DurationTypeId,
                OffsetTypeId = s.DelayTypeId
            })
            .ToList();

        var values = request.Steps
            .SelectMany(s => new[]
            {
                (s.DurationTypeId, s.DurationDays),
                (s.DelayTypeId, s.DelayDays)
            })
            .Where(x => x.Item1 > 0)
            .GroupBy(x => x.Item1)
            .ToDictionary(g => g.Key, g => g.First().Item2);

        var computation = ScheduleTimelineCalculator.Compute(request.StartDate, stepDefs, values);
        var summary = ScheduleTimelineCalculator.Summarize(computation, request.DeadlineDate);

        return new SchedulePreviewResponse
        {
            Steps = computation.Steps.Select(s => new SchedulePreviewStepResult
            {
                StepIndex = s.StepIndex,
                PlanStart = s.PlanStartDate.ToString("yyyy-MM-dd"),
                PlanEnd = s.PlanEndDate.ToString("yyyy-MM-dd"),
                ActualStart = s.ActualStartDate.ToString("yyyy-MM-dd"),
                ActualEnd = s.ActualEndDate.ToString("yyyy-MM-dd"),
                DurationDays = s.DurationDays,
                OffsetDays = s.OffsetDays
            }).ToList(),
            Summary = new SchedulePreviewSummary
            {
                BaselineCompletion = summary.BaselineCompletion.ToString("yyyy-MM-dd"),
                ActualCompletion = summary.ActualCompletion.ToString("yyyy-MM-dd"),
                TotalDurationDays = summary.TotalDurationDays,
                TotalOffsetDays = summary.TotalOffsetDays,
                IsOnTrack = summary.IsOnTrack,
                OverrunDays = summary.OverrunDays
            }
        };
    }
}
