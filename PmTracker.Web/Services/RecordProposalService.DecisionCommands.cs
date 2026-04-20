using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Records;

namespace PmTracker.Web.Services;

public sealed partial class RecordProposalService
{
    public async Task<int?> ApproveProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadPendingProposalForDecisionAsync(command, currentUser, ct);
        var oldProposalSnapshot = ProposalAuditSnapshot.FromEntity(proposal);

        await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        int? approvedRecordId = null;

        if (string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase))
        {
            var payload = DeserializePayload(proposal.PayloadJson);
            var createPayload = payload.CreateRecord
                ?? throw new InvalidOperationException("Payload návrhu založení záznamu je neplatný.");
            var saveCommand = _payloadMapper.BuildSaveCommand(createPayload);
            approvedRecordId = await _recordService.SaveRecordAsync(saveCommand, currentUser, ct);
        }
        else if (string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.SchedulePlanChange, StringComparison.OrdinalIgnoreCase))
        {
            await ApplyApprovedScheduleProposalAsync(proposal, currentUser, ct);
            approvedRecordId = proposal.ZaznamId;
        }
        else
        {
            throw new InvalidOperationException("Neznámý typ návrhu.");
        }

        proposal.Stav = RecordProposalStateCodes.Approved;
        proposal.DecidedByOsobaId = currentUser.OsobaId;
        proposal.DecidedAt = _timeProvider.GetUtcNow().UtcDateTime;
        proposal.ApprovedRecordId = approvedRecordId;
        await _dbContext.SaveChangesAsync(ct);
        _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Approve,
            AuditEntityType.RecordProposal,
            proposal.Id.ToString(CultureInfo.InvariantCulture),
            oldProposalSnapshot,
            ProposalAuditSnapshot.FromEntity(proposal)));
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return approvedRecordId;
    }

    public async Task RejectProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadPendingProposalForDecisionAsync(command, currentUser, ct);
        var oldProposalSnapshot = ProposalAuditSnapshot.FromEntity(proposal);
        await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        proposal.Stav = RecordProposalStateCodes.Rejected;
        proposal.DecidedByOsobaId = currentUser.OsobaId;
        proposal.DecidedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(ct);
        _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Reject,
            AuditEntityType.RecordProposal,
            proposal.Id.ToString(CultureInfo.InvariantCulture),
            oldProposalSnapshot,
            ProposalAuditSnapshot.FromEntity(proposal)));
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task RejectAndTakeOverCreateProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadPendingProposalForDecisionAsync(command, currentUser, ct);
        if (!string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Převzetí do formuláře je dostupné jen pro návrh založení záznamu.");
        }

        var oldProposalSnapshot = ProposalAuditSnapshot.FromEntity(proposal);
        await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        proposal.Stav = RecordProposalStateCodes.Rejected;
        proposal.DecidedByOsobaId = currentUser.OsobaId;
        proposal.DecidedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(ct);
        _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Reject,
            AuditEntityType.RecordProposal,
            proposal.Id.ToString(CultureInfo.InvariantCulture),
            oldProposalSnapshot,
            ProposalAuditSnapshot.FromEntity(proposal, "takeover")));
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task RejectAndEditProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadPendingProposalForDecisionAsync(command, currentUser, ct);
        var oldProposalSnapshot = ProposalAuditSnapshot.FromEntity(proposal);

        await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        proposal.Stav = RecordProposalStateCodes.Rejected;
        proposal.DecidedByOsobaId = currentUser.OsobaId;
        proposal.DecidedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(ct);
        _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Reject,
            AuditEntityType.RecordProposal,
            proposal.Id.ToString(CultureInfo.InvariantCulture),
            oldProposalSnapshot,
            ProposalAuditSnapshot.FromEntity(proposal, "edit")));
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // --- Private helpers used only by DecisionCommands ---

    private async Task<ZaznamNavrhEntity> LoadPendingProposalForDecisionAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct)
    {
        if (!await _authorizationPolicy.CanDecideProjectProposalAsync(command.ProjektId, currentUser, ct))
        {
            throw new InvalidOperationException("O návrzích může rozhodovat jen projektový manažer nebo administrátor projektu.");
        }

        var proposal = await LoadProposalAsync(command.ProjektId, command.ProposalId, ct);
        if (!string.Equals(proposal.Stav, RecordProposalStateCodes.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Rozhodnout lze jen o návrhu ve stavu čeká na rozhodnutí.");
        }

        return proposal;
    }

    private async Task ApplyApprovedScheduleProposalAsync(ZaznamNavrhEntity proposal, CurrentUserContextViewModel currentUser, CancellationToken ct)
    {
        if (!proposal.ZaznamId.HasValue)
        {
            throw new InvalidOperationException("Návrh změny harmonogramu není navázán na záznam.");
        }

        var payload = DeserializePayload(proposal.PayloadJson);
        var schedulePayload = payload.SchedulePlan
            ?? throw new InvalidOperationException("Payload návrhu harmonogramu je neplatný.");
        var record = await _dbContext.ProjektoveZaznamy
            .FirstOrDefaultAsync(x => x.Id == proposal.ZaznamId.Value && x.ProjektId == proposal.ProjektId, ct)
            ?? throw new InvalidOperationException($"Záznam {proposal.ZaznamId.Value} nebyl nalezen.");
        var oldRecordSnapshot = RecordAuditSnapshot.FromEntity(record);

        var scheduleTypeDefinitions = await ResolveScheduleTypeDefinitionsAsync(record, ct);
        var plannedTypeIds = scheduleTypeDefinitions
            .Select(x => x.DurationTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();
        var actualTypeIds = scheduleTypeDefinitions
            .Select(x => x.DelayTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();
        var allowedTypeIds = plannedTypeIds.Concat(actualTypeIds).ToHashSet();
        var submittedValues = (schedulePayload.ChangesSchedulePlan
                ? schedulePayload.PlannedHarmonogramHodnoty.Where(value => plannedTypeIds.Contains(value.TypId))
                : Enumerable.Empty<SaveRecordHarmonogramValueCommand>())
            .Concat(
                schedulePayload.ChangesScheduleActual
                    ? schedulePayload.ActualHarmonogramHodnoty.Where(value => actualTypeIds.Contains(value.TypId))
                    : Enumerable.Empty<SaveRecordHarmonogramValueCommand>())
            .GroupBy(value => value.TypId)
            .Select(group => new SaveRecordHarmonogramValueCommand
            {
                TypId = group.Key,
                Hodnota = plannedTypeIds.Contains(group.Key)
                    ? Math.Max(0, group.Last().Hodnota)
                    : group.Last().Hodnota
            })
            .ToList();
        var existingRows = await _dbContext.ZaznamHarmonogramHodnoty
            .Where(x => x.ZaznamId == record.Id && allowedTypeIds.Contains(x.TypId))
            .ToListAsync(ct);
        var oldScheduleSnapshot = existingRows.Count == 0
            ? null
            : RecordScheduleAuditSnapshot.FromEntities(record.Id, existingRows);
        var normalizedValues = submittedValues
            .Where(x => x.Hodnota > 0 || actualTypeIds.Contains(x.TypId))
            .ToDictionary(x => x.TypId, x => x.Hodnota);

        if (schedulePayload.ChangesTermDeadline)
        {
            record.DatumUkonceni = schedulePayload.TerminUkonceni.Date;
        }

        foreach (var row in existingRows)
        {
            if (normalizedValues.TryGetValue(row.TypId, out var updatedValue))
            {
                row.HodnotaInt = updatedValue;
                row.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                normalizedValues.Remove(row.TypId);
            }
            else if ((plannedTypeIds.Contains(row.TypId) && schedulePayload.ChangesSchedulePlan)
                || (actualTypeIds.Contains(row.TypId) && schedulePayload.ChangesScheduleActual))
            {
                _dbContext.ZaznamHarmonogramHodnoty.Remove(row);
            }
        }

        foreach (var plannedValue in normalizedValues)
        {
            _dbContext.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = record.Id,
                TypId = plannedValue.Key,
                HodnotaInt = plannedValue.Value,
                UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
            });
        }

        await _dbContext.SaveChangesAsync(ct);
        if (schedulePayload.ChangesTermDeadline || schedulePayload.ChangesSchedulePlan)
        {
            await _priorityMatrixRebuildService.RebuildForRecordAsync(record.Id, ct);
        }
        _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Update,
            AuditEntityType.Record,
            record.Id.ToString(CultureInfo.InvariantCulture),
            oldRecordSnapshot,
            RecordAuditSnapshot.FromEntity(record)));

        var newScheduleRows = await _dbContext.ZaznamHarmonogramHodnoty
            .AsNoTracking()
            .Where(x => x.ZaznamId == record.Id && allowedTypeIds.Contains(x.TypId))
            .ToListAsync(ct);
        var newScheduleSnapshot = newScheduleRows.Count == 0
            ? null
            : RecordScheduleAuditSnapshot.FromEntities(record.Id, newScheduleRows);
        if (oldScheduleSnapshot is not null || newScheduleSnapshot is not null)
        {
            _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                oldScheduleSnapshot is null ? AuditActionType.Create : AuditActionType.Update,
                AuditEntityType.RecordSchedule,
                record.Id.ToString(CultureInfo.InvariantCulture),
                oldScheduleSnapshot,
                newScheduleSnapshot));
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
