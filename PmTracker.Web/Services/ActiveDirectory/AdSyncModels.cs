using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed record AdSyncResult(
    DateTime StartedAt,
    DateTime FinishedAt,
    long DurationMs,
    int OkCount,
    int ErrorCount,
    int SkippedNoGuidCount,
    int NotFoundInAdCount,
    IReadOnlyList<AdSyncErrorItem> Errors)
{
    public static AdSyncResult Empty(DateTime startedAt)
        => new(startedAt, startedAt, 0, 0, 0, 0, 0, Array.Empty<AdSyncErrorItem>());
}

public sealed record AdSyncErrorItem(int OsobaId, Guid? GuidAd, string Reason);

public sealed record AdSinglePersonSyncResult(
    bool Success,
    bool FoundInAd,
    string? FailureReason);

/// <summary>
/// Reactive request pro jednotlivou osobu. DedupKey = OsobaId podle §13.1 spec:
/// opakovaný enqueue téže osoby, dokud consumer nedokončil, je no-op.
/// </summary>
public sealed record AdReactiveSyncRequest(int OsobaId, AdReactiveSource Source) : IHasDedupKey
{
    public object DedupKey => OsobaId;
}

public enum AdReactiveSource
{
    PersonPicked = 0,
    ManualUpdate = 1
}
