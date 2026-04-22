namespace PmTracker.Web.Models.ViewModels.Sync;

public sealed class SyncJobResultSummary
{
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public long? DurationMs { get; init; }
    public int? OkCount { get; init; }
    public int? ErrorCount { get; init; }
    public string? ErrorSummary { get; init; }
}
