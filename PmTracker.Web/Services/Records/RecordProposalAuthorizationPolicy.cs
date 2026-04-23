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
        var creatableSubsystemIds = await ResolveCreatableSubsystemIdsAsync(projectId, currentUser, ct);
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

    public Task<bool> CanDecideProjectProposalAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        // Per-action redesign 2026-04-23: hardkódovaný role-code check byl odstraněn
        // (Nález 9 v authz-ui-serverside-mismatch.md). Rozhodování o návrzích (accept)
        // je nyní vázáno na per-action klíč proposals.accept — seed řídí, které role ho
        // mají (dnes SUPERADMIN, APP_ADMIN, VP, ADM_PROJ, PROJ_MAN).
        if (currentUser.OsobaId <= 0)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(currentUser.HasPermission(PermissionKeys.ProposalsAccept, projectId));
    }

    private async Task<HashSet<int>> ResolveCreatableSubsystemIdsAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct)
    {
        if (currentUser.OsobaId <= 0)
        {
            return [];
        }

        // Per-action redesign 2026-04-23: admin bypass. Uživatel s proposals.edit.any
        // smí navrhovat pro jakýkoli subsystém projektu (nebo pro záznamy bez subsystému).
        if (currentUser.HasPermission(PermissionKeys.ProposalsEditAny, projectId))
        {
            return (await _dbContext.ProjektSubsystemy.AsNoTracking()
                .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
                .Select(x => x.SubsystemId)
                .ToListAsync(ct)).ToHashSet();
        }

        // Non-admin: VEDOUCI_SUBSYSTEMU / ZASTUPCE — jen subsystémy, kde má roli Lead/DeputyLead.
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
                    && assignment.OsobaId == currentUser.OsobaId
                    && !assignment.DatumOdebrani.HasValue
                    && roleIdSet.Contains(assignment.RoleSubsystemuId)
                select projectSubsystem.SubsystemId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();
    }
}
