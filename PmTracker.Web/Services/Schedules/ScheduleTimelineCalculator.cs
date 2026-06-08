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
        => Summarize(computation, deadline, today: null, lastStepHasActual: true);

    /// <summary>
    /// FIX 2026-05-05: rozšířená Summarize s projekcí uplynulého času. Pokud poslední krok schématu
    /// (= krok 10) NEMÁ vyplněnou skutečnost (<paramref name="lastStepHasActual"/> = false) a
    /// <paramref name="today"/> je dál než cumulative ActualEnd_10, projekce se posouvá na today.
    /// Důvod: současný calc kumuluje jen offsety z vyplněných kroků; pro nevyplněné kroky přidá
    /// jen plánované trvání (= +0 offset). User scenario: poslední vyplněný krok = krok 3 z 5.4.
    /// Krok 4-10 nevyplněno, dnes 5.5. Bez fixu projekce konce projektu = baseline_10 + offset_3
    /// (= ignoruje uplynulý měsíc, kdy nikdo nikam nepokročil). Po fixu: pokud baseline_10 + offset_3
    /// &lt; today, projekce = today (= projekt minimálně tolik, kolik už uplynulo).
    ///
    /// Backward compat: default <paramref name="today"/> = null + <paramref name="lastStepHasActual"/>
    /// = true zachová původní chování. Změnu používá jen <c>HarmonogramService.BuildHarmonogramSouhrn</c>
    /// pro souhrn pole v edit modalu.
    /// </summary>
    public static ScheduleTimelineSummary Summarize(
        ScheduleTimelineComputation computation,
        DateTime deadline,
        DateTime? today,
        bool lastStepHasActual)
    {
        var normalizedDeadline = deadline.Date;
        var baselineEnd = computation.Steps.Count == 0
            ? normalizedDeadline
            : computation.Steps[^1].PlanEndDate.Date;
        var rawActualEnd = computation.Steps.Count == 0
            ? normalizedDeadline
            : computation.Steps[^1].ActualEndDate.Date;

        // Promítnutí uplynulého času: pokud poslední krok nemá actual a today > cumulative ActualEnd,
        // projekt je reálně zpožděný oproti dnešnímu dni (kroky 4-10 nikdo nedokončil).
        var actualEnd = (lastStepHasActual || today is null)
            ? rawActualEnd
            : (rawActualEnd >= today.Value.Date ? rawActualEnd : today.Value.Date);

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
