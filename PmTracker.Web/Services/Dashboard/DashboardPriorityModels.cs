/// <summary>
/// Veřejné datové typy (records, interfaces) a konstanty pro prioritní matici dashboardu.
/// Tento soubor je základ — ostatní soubory v tomto adresáři na něj závisí.
/// </summary>

namespace PmTracker.Web.Services.Dashboard;

public sealed record PriorityRecordContext(
    int RecordId,
    bool IsTask,
    bool IsRunning,
    DateOnly? Deadline,
    DateOnly? NearestFuturePlannedMilestoneDate);

public sealed record PriorityUserRelevanceContext(
    int OsobaId,
    bool IsOwner,
    bool IsSubsystemLeadOrDeputy,
    bool IsCollaborator);

public sealed record PriorityScoreBreakdown(
    int Score,
    int RoleWeight,
    int DeadlineSignal,
    int MilestoneSignal);

public sealed record DashboardPriorityItem(
    int RecordId,
    int Score,
    int RoleWeight,
    int DeadlineSignal,
    int MilestoneSignal);

public interface IPriorityScoringService
{
    PriorityScoreBreakdown? ComputeScore(PriorityRecordContext record, PriorityUserRelevanceContext user, DateOnly today);
}

public interface IPriorityMatrixRebuildService
{
    Task FullRebuildAsync(CancellationToken ct = default);
    Task RebuildForRecordAsync(int recordId, CancellationToken ct = default);
    Task QueueRebuildForSubsystemAsync(int subsystemId, CancellationToken ct = default);
}

public interface IDashboardPriorityQuery
{
    Task<int> CountForUserAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<DashboardPriorityItem>> GetTopForUserAsync(int userId, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<DashboardPriorityItem>> GetAllForUserAsync(int userId, CancellationToken ct = default);
}

internal static class PriorityMatrixRebuildStatuses
{
    public const string Never = "NEVER";
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
    public const string Running = "RUNNING";
}
