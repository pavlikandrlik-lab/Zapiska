using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using System.Text.Json;

namespace PmTracker.Web.Services.Records;

public interface IPendingScheduleProposalLockEvaluator
{
    Task<PendingScheduleProposalLockState> EvaluateAsync(int recordId, CancellationToken ct = default);
}

public sealed record PendingScheduleProposalLockState(
    bool HasPendingProposal,
    int? ProposalId,
    string? Message,
    bool LocksTermDeadline,
    bool LocksSchedule,
    IReadOnlySet<Guid>? LockedManualKrokKeys = null);

public sealed class PendingScheduleProposalLockEvaluator : IPendingScheduleProposalLockEvaluator
{
    private readonly PmTrackerDbContext _dbContext;

    public PendingScheduleProposalLockEvaluator(PmTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PendingScheduleProposalLockState> EvaluateAsync(int recordId, CancellationToken ct = default)
    {
        if (recordId <= 0)
        {
            return new PendingScheduleProposalLockState(false, null, null, false, false);
        }

        var proposal = await _dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ZaznamId == recordId
                && x.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange
                && x.Stav == RecordProposalStateCodes.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.PayloadJson
            })
            .FirstOrDefaultAsync(ct);

        if (proposal is null)
        {
            return new PendingScheduleProposalLockState(false, null, null, false, false);
        }

        var payload = string.IsNullOrWhiteSpace(proposal.PayloadJson)
            ? null
            : JsonSerializer.Deserialize<RecordProposalPayload>(proposal.PayloadJson);
        var schedulePayload = payload?.SchedulePlan;
        var locksTermDeadline = schedulePayload?.ChangesTermDeadline ?? true;
        var locksSchedule = (schedulePayload?.ChangesSchedulePlan ?? false)
            || (schedulePayload?.ChangesScheduleActual ?? false);
        // Plán D: KrokKey, pro které pending návrh obsahuje ruční skutečnost.
        // UI je musí pro přímou editaci (schéma 1) uzamknout, jinak by přímý
        // zápis kolidoval s návrhem, který čeká na schválení.
        IReadOnlySet<Guid>? lockedManualKrokKeys = null;
        if (schedulePayload is not null && schedulePayload.ManualActualKroky.Count > 0)
        {
            lockedManualKrokKeys = schedulePayload.ManualActualKroky
                .Select(x => x.KrokKey)
                .Where(x => x != Guid.Empty)
                .ToHashSet();
            // Manuální kroky patří do actual části — zamykají schedule i pokud
            // v payloadu nejsou nastavené ChangesScheduleActual flagy.
            if (lockedManualKrokKeys.Count > 0)
            {
                locksSchedule = true;
            }
        }
        var message = locksTermDeadline && locksSchedule
            ? "Pro tento záznam už čeká návrh změny termínu a harmonogramu. Tyto části jsou do rozhodnutí uzamčené."
            : locksTermDeadline
                ? "Pro tento záznam už čeká návrh změny termínu. Termín ukončení je do rozhodnutí uzamčený."
                : "Pro tento záznam už čeká návrh změny harmonogramu. Harmonogram je do rozhodnutí uzamčený.";

        return new PendingScheduleProposalLockState(
            true,
            proposal.Id,
            message,
            locksTermDeadline,
            locksSchedule,
            lockedManualKrokKeys);
    }
}
