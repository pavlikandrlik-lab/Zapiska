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
    private static readonly IReadOnlyDictionary<int, IReadOnlyList<int>> Empty = new Dictionary<int, IReadOnlyList<int>>();

    private readonly PmTrackerDbContext _db;
    private readonly Dictionary<int, ProjectSubsystemRoles> _byProject = new();

    public ProjectRoleCache(PmTrackerDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetLeadEquivalentOsobaIdsBySubsystemAsync(
        int projectId, CancellationToken ct = default)
        => (await LoadAsync(projectId, ct).ConfigureAwait(false)).LeadEquivalent;

    public async Task<IReadOnlyList<int>> GetLeadEquivalentOsobaIdsAsync(
        int projectId, int subsystemId, CancellationToken ct = default)
    {
        var all = await GetLeadEquivalentOsobaIdsBySubsystemAsync(projectId, ct).ConfigureAwait(false);
        return all.TryGetValue(subsystemId, out var ids) ? ids : Array.Empty<int>();
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetSubsystemRoleOsobaIdsBySubsystemAsync(
        int projectId, CancellationToken ct = default)
        => (await LoadAsync(projectId, ct).ConfigureAwait(false)).AllRoles;

    public async Task<IReadOnlyList<int>> GetSubsystemRoleOsobaIdsAsync(
        int projectId, int subsystemId, CancellationToken ct = default)
    {
        var all = await GetSubsystemRoleOsobaIdsBySubsystemAsync(projectId, ct).ConfigureAwait(false);
        return all.TryGetValue(subsystemId, out var ids) ? ids : Array.Empty<int>();
    }

    private async Task<ProjectSubsystemRoles> LoadAsync(int projectId, CancellationToken ct)
    {
        if (_byProject.TryGetValue(projectId, out var cached))
        {
            return cached;
        }

        var roles = await _db.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead
                || x.Kod == SubsystemRoleCodes.DeputyLead
                || x.Kod == SubsystemRoleCodes.Methodik)
            .Select(x => new { x.Id, x.Kod })
            .ToListAsync(ct).ConfigureAwait(false);
        var leadRoleIds = roles
            .Where(x => x.Kod == SubsystemRoleCodes.Lead || x.Kod == SubsystemRoleCodes.DeputyLead)
            .Select(x => x.Id)
            .ToHashSet();
        var allRoleIds = roles.Select(x => x.Id).ToHashSet();

        var result = new ProjectSubsystemRoles(Empty, Empty);
        if (allRoleIds.Count > 0)
        {
            var subsystemIdByProjectSubsystemId = await _db.ProjektSubsystemy.AsNoTracking()
                .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
                .ToDictionaryAsync(x => x.Id, x => x.SubsystemId, ct).ConfigureAwait(false);
            var activeProjectSubsystemIds = subsystemIdByProjectSubsystemId.Keys.ToHashSet();

            if (activeProjectSubsystemIds.Count > 0)
            {
                var rows = await _db.ObsazeniSubsystemuProjektu.AsNoTracking()
                    .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                        && !x.DatumOdebrani.HasValue
                        && allRoleIds.Contains(x.RoleSubsystemuId))
                    .Select(x => new { x.ProjektSubsystemId, x.OsobaId, x.RoleSubsystemuId })
                    .ToListAsync(ct).ConfigureAwait(false);

                IReadOnlyDictionary<int, IReadOnlyList<int>> Group(IEnumerable<(int ProjektSubsystemId, int OsobaId)> source)
                    => source
                        .GroupBy(x => subsystemIdByProjectSubsystemId[x.ProjektSubsystemId])
                        .ToDictionary(
                            g => g.Key,
                            g => (IReadOnlyList<int>)g.Select(x => x.OsobaId).Distinct().OrderBy(x => x).ToList());

                result = new ProjectSubsystemRoles(
                    Group(rows.Where(x => leadRoleIds.Contains(x.RoleSubsystemuId)).Select(x => (x.ProjektSubsystemId, x.OsobaId))),
                    Group(rows.Select(x => (x.ProjektSubsystemId, x.OsobaId))));
            }
        }

        _byProject[projectId] = result;
        return result;
    }

    private sealed record ProjectSubsystemRoles(
        IReadOnlyDictionary<int, IReadOnlyList<int>> LeadEquivalent,
        IReadOnlyDictionary<int, IReadOnlyList<int>> AllRoles);
}
