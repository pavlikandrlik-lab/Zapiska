using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Schedules;

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

internal interface IPriorityMatrixRebuildQueue
{
    ValueTask EnqueueSubsystemAsync(int subsystemId, CancellationToken ct = default);
    IAsyncEnumerable<int> ReadAllAsync(CancellationToken ct);
    void MarkCompleted(int subsystemId);
}

internal static class PriorityMatrixRebuildStatuses
{
    public const string Never = "NEVER";
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
    public const string Running = "RUNNING";
}

internal sealed class PriorityScoringService : IPriorityScoringService
{
    private const int DeadlineSignalWeight = 3;
    private readonly DashboardPriorityOptions _options;

    public PriorityScoringService(IOptions<DashboardPriorityOptions> options)
    {
        _options = options.Value;
    }

    public PriorityScoreBreakdown? ComputeScore(PriorityRecordContext record, PriorityUserRelevanceContext user, DateOnly today)
    {
        if (!record.IsTask || !record.IsRunning)
        {
            return null;
        }

        var roleWeight = ResolveRoleWeight(user);
        if (roleWeight <= 0)
        {
            return null;
        }

        var deadlineSignal = ComputeDeadlineSignal(record.Deadline, today);
        var milestoneSignal = ComputeMilestoneSignal(record.NearestFuturePlannedMilestoneDate, today);
        var baseScore = (deadlineSignal * DeadlineSignalWeight) + milestoneSignal;
        var finalScore = (baseScore * roleWeight) / 100;

        return new PriorityScoreBreakdown(finalScore, roleWeight, deadlineSignal, milestoneSignal);
    }

    private int ResolveRoleWeight(PriorityUserRelevanceContext user)
    {
        if (user.IsOwner)
        {
            return 100;
        }

        if (user.IsSubsystemLeadOrDeputy)
        {
            return 70;
        }

        if (user.IsCollaborator)
        {
            return 40;
        }

        return 0;
    }

    private int ComputeDeadlineSignal(DateOnly? deadline, DateOnly today)
    {
        if (!deadline.HasValue)
        {
            return 0;
        }

        var deadlineDays = deadline.Value.DayNumber - today.DayNumber;
        if (deadlineDays >= 0)
        {
            return Math.Max(0, _options.PriorityHorizonDays - deadlineDays + 1);
        }

        return _options.PriorityHorizonDays + Math.Min(Math.Abs(deadlineDays), _options.PriorityOverdueCapDays);
    }

    private int ComputeMilestoneSignal(DateOnly? milestoneDate, DateOnly today)
    {
        if (!milestoneDate.HasValue)
        {
            return 0;
        }

        var milestoneDays = milestoneDate.Value.DayNumber - today.DayNumber;
        if (milestoneDays < 0)
        {
            return 0;
        }

        return Math.Max(0, _options.PriorityHorizonDays - milestoneDays + 1);
    }
}

internal sealed class DashboardPriorityQuery : IDashboardPriorityQuery
{
    private readonly PmTrackerDbContext _dbContext;

    public DashboardPriorityQuery(PmTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> CountForUserAsync(int userId, CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            return Task.FromResult(0);
        }

        return _dbContext.ZaznamPriorityUzivatelu.AsNoTracking()
            .CountAsync(x => x.OsobaId == userId, ct);
    }

    public Task<IReadOnlyList<DashboardPriorityItem>> GetTopForUserAsync(int userId, int limit, CancellationToken ct = default)
    {
        if (limit <= 0)
        {
            return Task.FromResult<IReadOnlyList<DashboardPriorityItem>>(Array.Empty<DashboardPriorityItem>());
        }

        return QueryAsync(userId, limit, ct);
    }

    public Task<IReadOnlyList<DashboardPriorityItem>> GetAllForUserAsync(int userId, CancellationToken ct = default)
        => QueryAsync(userId, null, ct);

    private async Task<IReadOnlyList<DashboardPriorityItem>> QueryAsync(int userId, int? take, CancellationToken ct)
    {
        if (userId <= 0)
        {
            return Array.Empty<DashboardPriorityItem>();
        }

        var query =
            from priority in _dbContext.ZaznamPriorityUzivatelu.AsNoTracking()
            join record in _dbContext.ProjektoveZaznamy.AsNoTracking() on priority.ZaznamId equals record.Id
            where priority.OsobaId == userId
            orderby priority.Score descending,
                record.DatumUkonceni,
                priority.RoleWeight descending,
                priority.ZaznamId
            select new DashboardPriorityItem(
                priority.ZaznamId,
                priority.Score,
                priority.RoleWeight,
                priority.DeadlineSignal,
                priority.MilestoneSignal);

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        return await query.ToListAsync(ct);
    }
}

internal sealed class PriorityMatrixRebuildQueue : IPriorityMatrixRebuildQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();
    private readonly ConcurrentDictionary<int, byte> _pendingSubsystemIds = new();

    public async ValueTask EnqueueSubsystemAsync(int subsystemId, CancellationToken ct = default)
    {
        if (subsystemId <= 0)
        {
            return;
        }

        if (!_pendingSubsystemIds.TryAdd(subsystemId, 0))
        {
            return;
        }

        await _channel.Writer.WriteAsync(subsystemId, ct);
    }

    public IAsyncEnumerable<int> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);

    public void MarkCompleted(int subsystemId)
    {
        _pendingSubsystemIds.TryRemove(subsystemId, out _);
    }
}

internal sealed class PriorityMatrixRebuildService : IPriorityMatrixRebuildService
{
    private sealed record RecordPriorityRow(
        int RecordId,
        int OsobaId,
        int Score,
        int RoleWeight,
        int DeadlineSignal,
        int MilestoneSignal,
        DateTime ComputedAt);

    private sealed record PriorityRecordRow(
        int Id,
        int ProjektId,
        int SubsystemId,
        int VlastnikId,
        string KategorieKod,
        string KategorieNazev,
        string? StavKod,
        DateTime DatumZalozeni,
        DateTime DatumUkonceni,
        int HarmonogramSablonaVerze);

    private sealed record LeadAssignmentRow(
        int ProjektId,
        int SubsystemId,
        int OsobaId);

    private readonly PmTrackerDbContext _dbContext;
    private readonly IPriorityScoringService _scoringService;
    private readonly IHarmonogramService _harmonogramService;
    private readonly IPriorityMatrixRebuildQueue _queue;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PriorityMatrixRebuildService> _logger;

    public PriorityMatrixRebuildService(
        PmTrackerDbContext dbContext,
        IPriorityScoringService scoringService,
        IHarmonogramService harmonogramService,
        IPriorityMatrixRebuildQueue queue,
        TimeProvider timeProvider,
        ILogger<PriorityMatrixRebuildService> logger)
    {
        _dbContext = dbContext;
        _scoringService = scoringService;
        _harmonogramService = harmonogramService;
        _queue = queue;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task FullRebuildAsync(CancellationToken ct = default)
    {
        await MarkFullRebuildRunningAsync(ct);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await RebuildRecordsCoreAsync(targetRecordIds: null, ct);
            await UpdateRebuildStateAsync(PriorityMatrixRebuildStatuses.Success, result.ProcessedRecordCount, stopwatch.ElapsedMilliseconds, ct);
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Priority matrix full rebuild finished. ProcessedRecords={ProcessedRecords} Created={Created} Updated={Updated} Deleted={Deleted} ElapsedMs={ElapsedMs}",
                result.ProcessedRecordCount,
                result.CreatedCount,
                result.UpdatedCount,
                result.DeletedCount,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (_dbContext.Database.CurrentTransaction is not null)
            {
                await _dbContext.Database.RollbackTransactionAsync(ct);
            }

            _dbContext.ChangeTracker.Clear();
            await UpdateRebuildStateAsync(PriorityMatrixRebuildStatuses.Failed, null, stopwatch.ElapsedMilliseconds, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogError(ex, "Priority matrix full rebuild failed after {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task RebuildForRecordAsync(int recordId, CancellationToken ct = default)
    {
        if (recordId <= 0)
        {
            return;
        }

        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        await using var tx = ownsTransaction
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;

        try
        {
            var result = await RebuildRecordsCoreAsync(new HashSet<int> { recordId }, ct);
            await _dbContext.SaveChangesAsync(ct);
            if (ownsTransaction && tx is not null)
            {
                await tx.CommitAsync(ct);
            }

            _logger.LogDebug(
                "Priority matrix record rebuild finished. RecordId={RecordId} Created={Created} Updated={Updated} Deleted={Deleted}",
                recordId,
                result.CreatedCount,
                result.UpdatedCount,
                result.DeletedCount);
        }
        catch
        {
            if (ownsTransaction && tx is not null)
            {
                await tx.RollbackAsync(ct);
            }

            throw;
        }
    }

    public Task QueueRebuildForSubsystemAsync(int subsystemId, CancellationToken ct = default)
        => _queue.EnqueueSubsystemAsync(subsystemId, ct).AsTask();

    internal async Task RebuildForSubsystemAsync(int subsystemId, CancellationToken ct = default)
    {
        if (subsystemId <= 0)
        {
            return;
        }

        var targetRecordIds = await _dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.SubsystemId == subsystemId)
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (targetRecordIds.Count == 0)
        {
            return;
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await RebuildRecordsCoreAsync(targetRecordIds.ToHashSet(), ct);
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _logger.LogInformation(
            "Priority matrix subsystem rebuild finished. SubsystemId={SubsystemId} ProcessedRecords={ProcessedRecords} Created={Created} Updated={Updated} Deleted={Deleted}",
            subsystemId,
            result.ProcessedRecordCount,
            result.CreatedCount,
            result.UpdatedCount,
            result.DeletedCount);
    }

    private async Task<PriorityRebuildResult> RebuildRecordsCoreAsync(HashSet<int>? targetRecordIds, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var computedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var candidateRows = await LoadPriorityRecordRowsAsync(targetRecordIds, ct);
        var activeRows = candidateRows
            .Where(row => RecordCategoryClassifier.IsTaskCategory(row.KategorieKod, row.KategorieNazev)
                && IsPriorityRunningState(row.StavKod))
            .ToList();
        var activeRecordIds = activeRows.Select(x => x.Id).ToHashSet();

        var collaborationByRecordId = await LoadCollaborationByRecordIdAsync(activeRecordIds, ct);
        var leadsByProjectSubsystem = await LoadLeadAssignmentsAsync(activeRows, ct);
        var scheduleValuesByRecordId = await LoadScheduleValuesByRecordIdAsync(activeRecordIds, ct);
        var schemaByVersion = await LoadSchemaByVersionAsync(activeRows, ct);
        var computedRows = new List<RecordPriorityRow>();

        foreach (var record in activeRows)
        {
            var scheduleValues = scheduleValuesByRecordId.GetValueOrDefault(record.Id)
                ?? new Dictionary<int, int>();
            var schema = schemaByVersion[record.HarmonogramSablonaVerze];
            var milestoneDate = ResolveNearestFutureMilestoneDate(today, record.DatumZalozeni, schema, scheduleValues);
            var recordContext = new PriorityRecordContext(
                record.Id,
                IsTask: true,
                IsRunning: true,
                Deadline: DateOnly.FromDateTime(record.DatumUkonceni.Date),
                NearestFuturePlannedMilestoneDate: milestoneDate);
            var relevantPeople = new HashSet<int>();
            if (record.VlastnikId > 0)
            {
                relevantPeople.Add(record.VlastnikId);
            }

            if (collaborationByRecordId.TryGetValue(record.Id, out var collaborators))
            {
                foreach (var collaborator in collaborators)
                {
                    relevantPeople.Add(collaborator);
                }
            }

            if (leadsByProjectSubsystem.TryGetValue((record.ProjektId, record.SubsystemId), out var subsystemLeads))
            {
                foreach (var subsystemLead in subsystemLeads)
                {
                    relevantPeople.Add(subsystemLead);
                }
            }

            foreach (var osobaId in relevantPeople)
            {
                var relevance = new PriorityUserRelevanceContext(
                    osobaId,
                    IsOwner: osobaId == record.VlastnikId,
                    IsSubsystemLeadOrDeputy: subsystemLeads?.Contains(osobaId) == true,
                    IsCollaborator: collaborators?.Contains(osobaId) == true);
                var breakdown = _scoringService.ComputeScore(recordContext, relevance, today);
                if (breakdown is null)
                {
                    continue;
                }

                computedRows.Add(new RecordPriorityRow(
                    record.Id,
                    osobaId,
                    breakdown.Score,
                    breakdown.RoleWeight,
                    breakdown.DeadlineSignal,
                    breakdown.MilestoneSignal,
                    computedAt));
            }
        }

        return await ApplyComputedRowsAsync(computedRows, targetRecordIds, activeRows.Count, ct);
    }

    private async Task<List<PriorityRecordRow>> LoadPriorityRecordRowsAsync(HashSet<int>? targetRecordIds, CancellationToken ct)
    {
        var recordsQuery = _dbContext.ProjektoveZaznamy.AsNoTracking();
        if (targetRecordIds is not null)
        {
            recordsQuery = recordsQuery.Where(record => targetRecordIds.Contains(record.Id));
        }

        var query =
            from record in recordsQuery
            join category in _dbContext.CiselnikKategoriiZaznamu.AsNoTracking() on record.KategorieId equals category.Id
            join state in _dbContext.CiselnikStavuUkolu.AsNoTracking() on record.StavUkoluId equals state.Id into stateGroup
            from state in stateGroup.DefaultIfEmpty()
            select new PriorityRecordRow(
                record.Id,
                record.ProjektId,
                record.SubsystemId,
                record.VlastnikId,
                category.Kod,
                category.Nazev,
                state != null ? state.Kod : null,
                record.DatumZalozeni,
                record.DatumUkonceni,
                record.HarmonogramSablonaVerze);

        return await query.ToListAsync(ct);
    }

    private static bool IsPriorityRunningState(string? stateCode)
        => string.Equals(stateCode, TaskStateCodes.Run, StringComparison.OrdinalIgnoreCase)
            || string.Equals(stateCode, TaskStateCodes.Open, StringComparison.OrdinalIgnoreCase);

    private async Task<Dictionary<int, HashSet<int>>> LoadCollaborationByRecordIdAsync(HashSet<int> recordIds, CancellationToken ct)
    {
        if (recordIds.Count == 0)
        {
            return new Dictionary<int, HashSet<int>>();
        }

        var rows = await _dbContext.ZaznamSpoluprace.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.OsobaId).ToHashSet());
    }

    private async Task<Dictionary<(int ProjektId, int SubsystemId), HashSet<int>>> LoadLeadAssignmentsAsync(
        IReadOnlyList<PriorityRecordRow> activeRows,
        CancellationToken ct)
    {
        if (activeRows.Count == 0)
        {
            return new Dictionary<(int ProjektId, int SubsystemId), HashSet<int>>();
        }

        var projectIds = activeRows.Select(x => x.ProjektId).Distinct().ToList();
        var subsystemIds = activeRows.Select(x => x.SubsystemId).Distinct().ToList();

        var rows = await (
                from assignment in _dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
                join projectSubsystem in _dbContext.ProjektSubsystemy.AsNoTracking() on assignment.ProjektSubsystemId equals projectSubsystem.Id
                join role in _dbContext.CiselnikRoliSubsystemu.AsNoTracking() on assignment.RoleSubsystemuId equals role.Id
                where !assignment.DatumOdebrani.HasValue
                    && !projectSubsystem.DatumOdebrani.HasValue
                    && projectIds.Contains(projectSubsystem.ProjektId)
                    && subsystemIds.Contains(projectSubsystem.SubsystemId)
                    && (role.Kod == SubsystemRoleCodes.Lead || role.Kod == SubsystemRoleCodes.DeputyLead)
                select new LeadAssignmentRow(
                    projectSubsystem.ProjektId,
                    projectSubsystem.SubsystemId,
                    assignment.OsobaId))
            .Distinct()
            .ToListAsync(ct);

        return rows
            .GroupBy(x => (x.ProjektId, x.SubsystemId))
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.OsobaId).ToHashSet());
    }

    private async Task<Dictionary<int, IReadOnlyDictionary<int, int>>> LoadScheduleValuesByRecordIdAsync(
        HashSet<int> recordIds,
        CancellationToken ct)
    {
        if (recordIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyDictionary<int, int>>();
        }

        var rows = await _dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<int, int>)group.ToDictionary(x => x.TypId, x => x.HodnotaInt));
    }

    private async Task<Dictionary<int, HarmonogramSchemaDefinition>> LoadSchemaByVersionAsync(
        IReadOnlyList<PriorityRecordRow> activeRows,
        CancellationToken ct)
    {
        var result = new Dictionary<int, HarmonogramSchemaDefinition>();
        foreach (var schemaVersion in activeRows.Select(x => x.HarmonogramSablonaVerze).Distinct())
        {
            result[schemaVersion] = await _harmonogramService.GetSchemaForRecordAsync(schemaVersion, ct);
        }

        return result;
    }

    private static DateOnly? ResolveNearestFutureMilestoneDate(
        DateOnly today,
        DateTime startDate,
        HarmonogramSchemaDefinition schema,
        IReadOnlyDictionary<int, int> values)
    {
        if (schema.Kroky.Count == 0)
        {
            return null;
        }

        var computation = ScheduleTimelineCalculator.Compute(
            startDate.Date,
            schema.Kroky
                .OrderBy(x => x.KrokIndex)
                .Select(step => new ScheduleTimelineStepDefinition
                {
                    StepIndex = step.KrokIndex,
                    Code = step.Kod,
                    Name = step.Nazev,
                    ColorHex = step.BarvaHex,
                    DurationTypeId = step.TrvaniTypId,
                    OffsetTypeId = step.ZpozdeniTypId
                })
                .ToList(),
            values);
        var nextStep = computation.Steps
            .Where(step => DateOnly.FromDateTime(step.PlanEndDate.Date) >= today)
            .OrderBy(step => step.PlanEndDate)
            .FirstOrDefault();

        return nextStep is null
            ? null
            : DateOnly.FromDateTime(nextStep.PlanEndDate.Date);
    }

    private async Task<PriorityRebuildResult> ApplyComputedRowsAsync(
        IReadOnlyList<RecordPriorityRow> computedRows,
        HashSet<int>? targetRecordIds,
        int processedRecordCount,
        CancellationToken ct)
    {
        var existingQuery = _dbContext.ZaznamPriorityUzivatelu.AsTracking();
        if (targetRecordIds is not null)
        {
            existingQuery = existingQuery.Where(x => targetRecordIds.Contains(x.ZaznamId));
        }

        var existingRows = await existingQuery.ToListAsync(ct);
        var computedByKey = computedRows.ToDictionary(x => (x.RecordId, x.OsobaId));
        var createdCount = 0;
        var updatedCount = 0;
        var deletedCount = 0;

        foreach (var existingRow in existingRows)
        {
            var key = (existingRow.ZaznamId, existingRow.OsobaId);
            if (!computedByKey.TryGetValue(key, out var computed))
            {
                _dbContext.ZaznamPriorityUzivatelu.Remove(existingRow);
                deletedCount++;
                continue;
            }

            existingRow.Score = computed.Score;
            existingRow.ComputedAt = computed.ComputedAt;
            existingRow.RoleWeight = computed.RoleWeight;
            existingRow.DeadlineSignal = computed.DeadlineSignal;
            existingRow.MilestoneSignal = computed.MilestoneSignal;
            updatedCount++;
            computedByKey.Remove(key);
        }

        foreach (var computed in computedByKey.Values)
        {
            _dbContext.ZaznamPriorityUzivatelu.Add(new ZaznamPriorityUzivateleEntity
            {
                ZaznamId = computed.RecordId,
                OsobaId = computed.OsobaId,
                Score = computed.Score,
                ComputedAt = computed.ComputedAt,
                RoleWeight = computed.RoleWeight,
                DeadlineSignal = computed.DeadlineSignal,
                MilestoneSignal = computed.MilestoneSignal
            });
            createdCount++;
        }

        return new PriorityRebuildResult(processedRecordCount, createdCount, updatedCount, deletedCount);
    }

    private async Task MarkFullRebuildRunningAsync(CancellationToken ct)
    {
        var state = await GetOrCreateRebuildStateAsync(ct);
        state.LastFullRebuildStatus = PriorityMatrixRebuildStatuses.Running;
        state.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task UpdateRebuildStateAsync(
        string status,
        int? taskCount,
        long? durationMs,
        CancellationToken ct)
    {
        var state = await GetOrCreateRebuildStateAsync(ct);
        state.LastFullRebuildStatus = status;
        state.LastFullRebuildAt = _timeProvider.GetUtcNow().UtcDateTime;
        state.LastFullRebuildDurationMs = durationMs;
        state.LastFullRebuildTaskCount = taskCount;
        state.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
    }

    private async Task<ZaznamPriorityRebuildStateEntity> GetOrCreateRebuildStateAsync(CancellationToken ct)
    {
        var state = await _dbContext.ZaznamPriorityRebuildState
            .FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (state is not null)
        {
            return state;
        }

        state = new ZaznamPriorityRebuildStateEntity
        {
            Id = 1,
            LastFullRebuildStatus = PriorityMatrixRebuildStatuses.Never,
            UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _dbContext.ZaznamPriorityRebuildState.Add(state);
        await _dbContext.SaveChangesAsync(ct);
        return state;
    }

    private sealed record PriorityRebuildResult(
        int ProcessedRecordCount,
        int CreatedCount,
        int UpdatedCount,
        int DeletedCount);
}

internal sealed class PriorityMatrixBootstrapHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DashboardPriorityOptions _options;
    private readonly ILogger<PriorityMatrixBootstrapHostedService> _logger;

    public PriorityMatrixBootstrapHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<DashboardPriorityOptions> options,
        ILogger<PriorityMatrixBootstrapHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_options.BootstrapFullRebuildOnStartup)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        var state = await dbContext.ZaznamPriorityRebuildState
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (state is not null
            && !string.Equals(state.LastFullRebuildStatus, PriorityMatrixRebuildStatuses.Never, StringComparison.OrdinalIgnoreCase)
            && state.LastFullRebuildAt.HasValue)
        {
            return;
        }

        _logger.LogInformation("Priority matrix bootstrap full rebuild started.");
        await scope.ServiceProvider.GetRequiredService<IPriorityMatrixRebuildService>().FullRebuildAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed class PriorityMatrixNightlyRebuildHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DashboardPriorityOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PriorityMatrixNightlyRebuildHostedService> _logger;

    public PriorityMatrixNightlyRebuildHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<DashboardPriorityOptions> options,
        TimeProvider timeProvider,
        ILogger<PriorityMatrixNightlyRebuildHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nightlyTime = _options.GetNightlyRebuildTime();

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _timeProvider.GetLocalNow();
            var nextRun = new DateTimeOffset(
                now.Year,
                now.Month,
                now.Day,
                nightlyTime.Hour,
                nightlyTime.Minute,
                0,
                now.Offset);
            if (nextRun <= now)
            {
                nextRun = nextRun.AddDays(1);
            }

            var delay = nextRun - now;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IPriorityMatrixRebuildService>().FullRebuildAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nightly priority matrix rebuild failed.");
            }
        }
    }
}

internal sealed class PriorityMatrixQueuedRebuildHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPriorityMatrixRebuildQueue _queue;
    private readonly ILogger<PriorityMatrixQueuedRebuildHostedService> _logger;

    public PriorityMatrixQueuedRebuildHostedService(
        IServiceScopeFactory scopeFactory,
        IPriorityMatrixRebuildQueue queue,
        ILogger<PriorityMatrixQueuedRebuildHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var subsystemId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<PriorityMatrixRebuildService>();
                await service.RebuildForSubsystemAsync(subsystemId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queued priority matrix rebuild failed for subsystem {SubsystemId}.", subsystemId);
            }
            finally
            {
                _queue.MarkCompleted(subsystemId);
            }
        }
    }
}
