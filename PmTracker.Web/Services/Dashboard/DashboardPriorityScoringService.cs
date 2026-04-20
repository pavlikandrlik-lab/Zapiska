/// <summary>
/// Čistá výpočetní logika prioritního skóre uživatele pro záznam.
/// Žádné EF závislosti — pouze pure-compute přes IOptions.
/// </summary>

using Microsoft.Extensions.Options;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Dashboard;

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
