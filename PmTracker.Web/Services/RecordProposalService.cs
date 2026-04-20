using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Dashboard;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services;

public sealed partial class RecordProposalService : IRecordProposalService
{
    private sealed record ProposalListRow(
        int Id,
        int ProjektId,
        int? ZaznamId,
        int SubsystemId,
        string TypNavrhu,
        string Stav,
        string PayloadJson,
        int CreatedByOsobaId,
        DateTime CreatedAt,
        int? DecidedByOsobaId,
        DateTime? DecidedAt,
        int? ApprovedRecordId);

    private sealed record ProposalRecordRow(
        int Id,
        string CisloViditelne,
        string Nazev,
        string? Cil,
        DateTime DatumZalozeni,
        DateTime TerminUkonceni);

    private readonly PmTrackerDbContext _dbContext;
    private readonly IRecordService _recordService;
    private readonly IRecordProposalAuthorizationPolicy _authorizationPolicy;
    private readonly IPendingScheduleProposalLockEvaluator _pendingScheduleProposalLockEvaluator;
    private readonly RecordProposalPayloadMapper _payloadMapper;
    private readonly IHarmonogramService _harmonogramService;
    private readonly IPriorityMatrixRebuildService _priorityMatrixRebuildService;
    private readonly IAuditWriteService _auditWriteService;
    private readonly TimeProvider _timeProvider;

    public RecordProposalService(
        PmTrackerDbContext dbContext,
        IRecordService recordService,
        IRecordProposalAuthorizationPolicy authorizationPolicy,
        IPendingScheduleProposalLockEvaluator pendingScheduleProposalLockEvaluator,
        RecordProposalPayloadMapper payloadMapper,
        IHarmonogramService harmonogramService,
        IPriorityMatrixRebuildService priorityMatrixRebuildService,
        IAuditWriteService auditWriteService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _recordService = recordService;
        _authorizationPolicy = authorizationPolicy;
        _pendingScheduleProposalLockEvaluator = pendingScheduleProposalLockEvaluator;
        _payloadMapper = payloadMapper;
        _harmonogramService = harmonogramService;
        _priorityMatrixRebuildService = priorityMatrixRebuildService;
        _auditWriteService = auditWriteService;
        _timeProvider = timeProvider;
    }

    // --- Shared helpers (called from multiple partials) ---

    private async Task<ZaznamNavrhEntity> LoadProposalAsync(int projectId, int proposalId, CancellationToken ct)
    {
        return await _dbContext.ZaznamNavrhy
            .FirstOrDefaultAsync(x => x.Id == proposalId && x.ProjektId == projectId, ct)
            ?? throw new InvalidOperationException($"Návrh {proposalId} nebyl nalezen.");
    }

    private RecordProposalPayload DeserializePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new RecordProposalPayload();
        }

        return JsonSerializer.Deserialize<RecordProposalPayload>(payloadJson) ?? new RecordProposalPayload();
    }

    private async Task<IReadOnlyList<RecordScheduleTypeDefinition>> ResolveScheduleTypeDefinitionsAsync(ProjektovyZaznamEntity record, CancellationToken ct)
    {
        var schema = await _harmonogramService.GetSchemaForRecordAsync(record, ct);
        return _harmonogramService.BuildRecordScheduleTypeDefinitions(schema);
    }

    private async Task<HashSet<int>> ResolvePlannedTypeIdsAsync(ProjektovyZaznamEntity record, CancellationToken ct)
    {
        var schema = await _harmonogramService.GetSchemaForRecordAsync(record, ct);
        return _harmonogramService.BuildRecordScheduleTypeDefinitions(schema)
            .Select(x => x.DurationTypeId)
            .Where(x => x > 0)
            .ToHashSet();
    }
}
