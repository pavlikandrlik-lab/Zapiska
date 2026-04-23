using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

/// <summary>
/// Scoped implementace <see cref="IProjectRoleCache"/>. Drží per-projectId snapshot po
/// celou dobu HTTP requestu. Viz <see cref="IProjectRoleCache"/> pro motivaci a kontext.
/// </summary>
public sealed class ProjectRoleCache : IProjectRoleCache
{
    private readonly PmTrackerDbContext _db;
    private readonly Dictionary<int, IReadOnlyDictionary<int, IReadOnlyList<int>>> _byProject = new();

    public ProjectRoleCache(PmTrackerDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetLeadEquivalentOsobaIdsBySubsystemAsync(
        int projectId, CancellationToken ct = default)
    {
        if (_byProject.TryGetValue(projectId, out var cached))
        {
            return cached;
        }

        var leadRoleIds = (await _db.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead || x.Kod == SubsystemRoleCodes.DeputyLead)
            .Select(x => x.Id)
            .ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet();

        IReadOnlyDictionary<int, IReadOnlyList<int>> result;
        if (leadRoleIds.Count == 0)
        {
            result = new Dictionary<int, IReadOnlyList<int>>();
        }
        else
        {
            var activeProjectSubsystems = await _db.ProjektSubsystemy.AsNoTracking()
                .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
                .Select(x => new { x.Id, x.SubsystemId })
                .ToListAsync(ct).ConfigureAwait(false);
            var subsystemIdByProjectSubsystemId = activeProjectSubsystems
                .ToDictionary(x => x.Id, x => x.SubsystemId);
            var activeProjectSubsystemIds = subsystemIdByProjectSubsystemId.Keys.ToHashSet();

            if (activeProjectSubsystemIds.Count == 0)
            {
                result = new Dictionary<int, IReadOnlyList<int>>();
            }
            else
            {
                var rows = await _db.ObsazeniSubsystemuProjektu.AsNoTracking()
                    .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                        && !x.DatumOdebrani.HasValue
                        && leadRoleIds.Contains(x.RoleSubsystemuId))
                    .Select(x => new { x.ProjektSubsystemId, x.OsobaId })
                    .ToListAsync(ct).ConfigureAwait(false);

                result = rows
                    .GroupBy(x => subsystemIdByProjectSubsystemId[x.ProjektSubsystemId])
                    .ToDictionary(
                        g => g.Key,
                        g => (IReadOnlyList<int>)g.Select(x => x.OsobaId).Distinct().OrderBy(x => x).ToList());
            }
        }

        _byProject[projectId] = result;
        return result;
    }

    public async Task<IReadOnlyList<int>> GetLeadEquivalentOsobaIdsAsync(
        int projectId, int subsystemId, CancellationToken ct = default)
    {
        var all = await GetLeadEquivalentOsobaIdsBySubsystemAsync(projectId, ct).ConfigureAwait(false);
        return all.TryGetValue(subsystemId, out var ids) ? ids : Array.Empty<int>();
    }
}
