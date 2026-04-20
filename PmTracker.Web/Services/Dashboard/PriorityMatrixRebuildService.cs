/// <summary>
/// Rebuild service pro prioritní matici (EF zápisy) + in-memory fronta subsystémů.
/// Obsahuje: IPriorityMatrixRebuildQueue (internal), PriorityMatrixRebuildQueue, PriorityMatrixRebuildService.
/// </summary>

using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services.Dashboard;

internal interface IPriorityMatrixRebuildQueue
{
    ValueTask EnqueueSubsystemAsync(int subsystemId, CancellationToken ct = default);
    IAsyncEnumerable<int> ReadAllAsync(CancellationToken ct);
    void MarkCompleted(int subsystemId);
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
