using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Common;

internal sealed record ProjectSubsystemOrderingRow(
    int ProjektSubsystemId,
    int SubsystemId,
    int Poradi,
    string Kod,
    string Nazev,
    DateTime DatumPrirazeni);

internal static class ProjectSubsystemOrderingQuery
{
    public static Task<List<ProjectSubsystemOrderingRow>> LoadActiveRowsAsync(
        PmTrackerDbContext dbContext,
        int projectId,
        IReadOnlyCollection<int>? subsystemIds = null,
        CancellationToken ct = default)
    {
        var normalizedSubsystemIds = subsystemIds?
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        var query =
            from mapping in dbContext.ProjektSubsystemy.AsNoTracking()
            join subsystem in dbContext.Subsystemy.AsNoTracking() on mapping.SubsystemId equals subsystem.Id
            where mapping.ProjektId == projectId && !mapping.DatumOdebrani.HasValue
            select new { mapping, subsystem };

        if (normalizedSubsystemIds is { Length: > 0 })
        {
            query = query.Where(x => normalizedSubsystemIds.Contains(x.mapping.SubsystemId));
        }

        return query
            .OrderBy(x => x.mapping.Poradi)
            .ThenBy(x => x.subsystem.Kod)
            .ThenBy(x => x.subsystem.Nazev)
            .ThenBy(x => x.mapping.Id)
            .Select(x => new ProjectSubsystemOrderingRow(
                x.mapping.Id,
                x.mapping.SubsystemId,
                x.mapping.Poradi,
                x.subsystem.Kod ?? "-",
                x.subsystem.Nazev ?? "-",
                x.mapping.DatumPrirazeni))
            .ToListAsync(ct);
    }

    public static async Task<Dictionary<int, ProjectSubsystemOrderingRow>> LoadActiveBySubsystemIdAsync(
        PmTrackerDbContext dbContext,
        int projectId,
        IReadOnlyCollection<int>? subsystemIds = null,
        CancellationToken ct = default)
    {
        return (await LoadActiveRowsAsync(dbContext, projectId, subsystemIds, ct))
            .ToDictionary(item => item.SubsystemId);
    }

    public static async Task<Dictionary<int, int>> LoadActiveOrderBySubsystemIdAsync(
        PmTrackerDbContext dbContext,
        int projectId,
        IReadOnlyCollection<int>? subsystemIds = null,
        CancellationToken ct = default)
    {
        return (await LoadActiveRowsAsync(dbContext, projectId, subsystemIds, ct))
            .ToDictionary(item => item.SubsystemId, item => item.Poradi);
    }

    public static async Task<int> ResolveNextActiveOrderAsync(PmTrackerDbContext dbContext, int projectId, CancellationToken ct = default)
    {
        var maxOrder = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .Select(x => (int?)x.Poradi)
            .MaxAsync(ct);

        return (maxOrder ?? 0) + 1;
    }
}
