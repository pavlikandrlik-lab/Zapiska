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
    IReadOnlySet<int>? LockedManualKrokKeys = null);

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

        // FIX 2026-05-01 (round 3 #18): safe deserialize. Pokud payload je malformed (DB
        // pollution, admin SQL edit, schema migration broken), vrátíme defensive lock state
        // místo unhandled JsonException → user dostane meaningful error v UI místo 500.
        RecordProposalPayload? payload = null;
        if (!string.IsNullOrWhiteSpace(proposal.PayloadJson))
        {
            try
            {
                payload = JsonSerializer.Deserialize<RecordProposalPayload>(proposal.PayloadJson);
            }
            catch (JsonException)
            {
                // Malformed payload → návrh je v inconsistent stavu, full lock + user-friendly message.
                return new PendingScheduleProposalLockState(
                    HasPendingProposal: true,
                    ProposalId: proposal.Id,
                    Message: $"Návrh #{proposal.Id} je v neplatném stavu (malformed payload). Kontaktuj správce.",
                    LocksTermDeadline: true,
                    LocksSchedule: true);
            }
        }
        var schedulePayload = payload?.SchedulePlan;
        var locksTermDeadline = schedulePayload?.ChangesTermDeadline ?? true;
        var locksSchedule = (schedulePayload?.ChangesSchedulePlan ?? false)
            || (schedulePayload?.ChangesScheduleActual ?? false);
        // Plán D: KrokKey, pro které pending návrh obsahuje ruční skutečnost.
        // UI je musí pro přímou editaci (schéma 1) uzamknout, jinak by přímý
        // zápis kolidoval s návrhem, který čeká na schválení.
        IReadOnlySet<int>? lockedManualKrokKeys = null;
        if (schedulePayload is not null && schedulePayload.ManualActualKroky.Count > 0)
        {
            lockedManualKrokKeys = schedulePayload.ManualActualKroky
                .Select(x => x.Poradi)
                .Where(x => x is >= 1 and <= 10)
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
