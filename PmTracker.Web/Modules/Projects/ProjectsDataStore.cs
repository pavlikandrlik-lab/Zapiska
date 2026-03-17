using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Modules.Projects;

public sealed class ProjectsDataStore(IPmTrackerDataStore dataStore) : IProjectsDataStore
{
    public bool ProjektExists(int id) => dataStore.ProjektExists(id);

    public IReadOnlyList<ProjektListItemViewModel> BuildProjektyList() => dataStore.BuildProjektyList();

    public ProjektDetailViewModel BuildProjektDetail(int id) => dataStore.BuildProjektDetail(id);

    public CiselnikDetailViewModel BuildCiselnikDetail(string key, CurrentUserContextViewModel currentUser) => dataStore.BuildCiselnikDetail(key, currentUser);

    public int SaveProject(SaveProjectCommand command, CurrentUserContextViewModel currentUser) => dataStore.SaveProject(command, currentUser);

    public void SoftDeleteProject(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser) => dataStore.SoftDeleteProject(command, currentUser);

    public void SaveTeamMember(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser) => dataStore.SaveTeamMember(command, currentUser);

    public void RemoveTeamMember(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser) => dataStore.RemoveTeamMember(command, currentUser);

    public void AssignProjectRole(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser) => dataStore.AssignProjectRole(command, currentUser);

    public void DeactivateProjectRole(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser) => dataStore.DeactivateProjectRole(command, currentUser);

    public void AssignProjectSubsystem(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser) => dataStore.AssignProjectSubsystem(command, currentUser);

    public void DeactivateProjectSubsystem(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser) => dataStore.DeactivateProjectSubsystem(command, currentUser);

    public void AssignProjectSubsystemRole(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser) => dataStore.AssignProjectSubsystemRole(command, currentUser);

    public void DeactivateProjectSubsystemRole(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser) => dataStore.DeactivateProjectSubsystemRole(command, currentUser);
}
