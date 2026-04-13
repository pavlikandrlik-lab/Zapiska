using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records;

public interface IPendingScheduleProposalLockEvaluator
{
    Task<PendingScheduleProposalLockState> EvaluateAsync(int recordId, CancellationToken ct = default);
}

public sealed record PendingScheduleProposalLockState(
    bool HasPendingProposal,
    int? ProposalId,
    string? Message);

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
            return new PendingScheduleProposalLockState(false, null, null);
        }

        var proposalId = await _dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ZaznamId == recordId
                && x.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange
                && x.Stav == RecordProposalStateCodes.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

        return proposalId.HasValue
            ? new PendingScheduleProposalLockState(
                true,
                proposalId.Value,
                "Pro tento záznam už čeká návrh změny termínu nebo plánové části harmonogramu. Tyto pole jsou do rozhodnutí uzamčená.")
            : new PendingScheduleProposalLockState(false, null, null);
    }
}
