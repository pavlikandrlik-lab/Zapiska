using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records;

public interface IRecordProposalAuthorizationPolicy
{
    Task<RecordProposalProjectAuthorizationResult> EvaluateProjectAccessAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task<RecordProposalRecordAuthorizationResult> EvaluateRecordAccessAsync(int projectId, int recordId, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task<bool> CanDecideProjectProposalAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}

public sealed record RecordProposalProjectAuthorizationResult(
    bool CanViewTab,
    bool CanDecide,
    IReadOnlySet<int> CreatableSubsystemIds);

public sealed record RecordProposalRecordAuthorizationResult(
    bool CanViewTab,
    bool CanDecide,
    bool CanCreateScheduleProposal,
    int? RecordSubsystemId,
    IReadOnlySet<int> CreatableSubsystemIds);

public sealed class RecordProposalAuthorizationPolicy : IRecordProposalAuthorizationPolicy
{
    private readonly PmTrackerDbContext _dbContext;

    public RecordProposalAuthorizationPolicy(PmTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RecordProposalProjectAuthorizationResult> EvaluateProjectAccessAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var creatableSubsystemIds = await ResolveCreatableSubsystemIdsAsync(projectId, currentUser.OsobaId, ct);
        var canDecide = await CanDecideProjectProposalAsync(projectId, currentUser, ct);
        return new RecordProposalProjectAuthorizationResult(
            CanViewTab: canDecide || creatableSubsystemIds.Count > 0,
            CanDecide: canDecide,
            CreatableSubsystemIds: creatableSubsystemIds);
    }

    public async Task<RecordProposalRecordAuthorizationResult> EvaluateRecordAccessAsync(int projectId, int recordId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var projectAccess = await EvaluateProjectAccessAsync(projectId, currentUser, ct);
        var recordSubsystemId = await _dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == recordId && x.ProjektId == projectId)
            .Select(x => (int?)x.SubsystemId)
            .FirstOrDefaultAsync(ct);

        var canCreateScheduleProposal = recordSubsystemId.HasValue
            && projectAccess.CreatableSubsystemIds.Contains(recordSubsystemId.Value);

        return new RecordProposalRecordAuthorizationResult(
            projectAccess.CanViewTab,
            projectAccess.CanDecide,
            canCreateScheduleProposal,
            recordSubsystemId,
            projectAccess.CreatableSubsystemIds);
    }

    public async Task<bool> CanDecideProjectProposalAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        if (currentUser.OsobaId <= 0)
        {
            return false;
        }

        var activeProjectRoleCodes = await (
                from assignment in _dbContext.ObsazeniProjektu.AsNoTracking()
                join role in _dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals role.Id
                where assignment.ProjektId == projectId
                    && assignment.OsobaId == currentUser.OsobaId
                    && !assignment.DatumOdebrani.HasValue
                select role.Kod)
            .ToListAsync(ct);

        return activeProjectRoleCodes.Any(code =>
            string.Equals(code, ProjectRoleCodes.ProjectAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, ProjectRoleCodes.ProjectManager, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<HashSet<int>> ResolveCreatableSubsystemIdsAsync(int projectId, int osobaId, CancellationToken ct)
    {
        if (osobaId <= 0)
        {
            return [];
        }

        var subsystemRoleIds = await _dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead || x.Kod == SubsystemRoleCodes.DeputyLead)
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (subsystemRoleIds.Count == 0)
        {
            return [];
        }

        var roleIdSet = subsystemRoleIds.ToHashSet();
        return (await (
                from assignment in _dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
                join projectSubsystem in _dbContext.ProjektSubsystemy.AsNoTracking() on assignment.ProjektSubsystemId equals projectSubsystem.Id
                where projectSubsystem.ProjektId == projectId
                    && !projectSubsystem.DatumOdebrani.HasValue
                    && assignment.OsobaId == osobaId
                    && !assignment.DatumOdebrani.HasValue
                    && roleIdSet.Contains(assignment.RoleSubsystemuId)
                select projectSubsystem.SubsystemId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();
    }
}
