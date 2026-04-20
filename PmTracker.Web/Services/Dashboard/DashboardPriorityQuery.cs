/// <summary>
/// EF read-only dotazy pro prioritní matici dashboardu.
/// Implementace IDashboardPriorityQuery — pouze čtení, bez zápisů do DB.
/// </summary>

using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Dashboard;

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
