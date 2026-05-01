namespace PmTracker.Web.Models.ViewModels.Sync;

public sealed class SyncJobSettingsCardViewModel
{
    public required string JobKey { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }

    public bool IsEnabled { get; set; }
    public int PeriodMinutes { get; set; }
    public DateTimeOffset AnchorAt { get; set; }

    public DateTime? LastRunAt { get; init; }
    public string? LastTriggerKind { get; init; }
    public SyncJobResultSummary? LastResult { get; init; }

    public bool IsRunning { get; init; }
    public DateTime? RunStartedAt { get; init; }

    public bool CanManage { get; init; }

    /// <summary>
    /// Read-only ukázka nejbližších naplánovaných spuštění (typicky 4) — vypočítáno
    /// z AnchorAt + PeriodMinutes. Slouží jen jako vizuální feedback, mění se až
    /// po uložení karty. Pokud job není IsEnabled, je seznam prázdný.
    /// </summary>
    public IReadOnlyList<DateTimeOffset> UpcomingRuns { get; init; } = Array.Empty<DateTimeOffset>();
}
