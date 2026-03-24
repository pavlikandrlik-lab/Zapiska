using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

public interface IProjectService
{
    Task<bool> ProjektExistsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ProjektListItemViewModel>> BuildProjektyListAsync(CancellationToken ct = default);
    Task<ProjektDetailViewModel> BuildProjektDetailAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<LookupOptionViewModel>> BuildProjectStatusOptionsAsync(CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveTeamMemberAsync(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task RemoveTeamMemberAsync(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);

    Task<int> SaveProjectAsync(SaveProjectCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SoftDeleteProjectAsync(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);

    Task AssignProjectRoleAsync(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeactivateProjectRoleAsync(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task AssignProjectSubsystemAsync(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeactivateProjectSubsystemAsync(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task AssignProjectSubsystemRoleAsync(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeactivateProjectSubsystemRoleAsync(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}
