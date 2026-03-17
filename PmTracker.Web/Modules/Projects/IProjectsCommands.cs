using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects;

public interface IProjectsCommands
{
    int SaveProject(SaveProjectCommand command, CurrentUserContextViewModel currentUser);
    void SoftDeleteProject(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser);
    void SaveTeamMember(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser);
    void RemoveTeamMember(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser);
    void AssignProjectRole(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser);
    void DeactivateProjectRole(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser);
    void AssignProjectSubsystem(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser);
    void DeactivateProjectSubsystem(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser);
    void AssignProjectSubsystemRole(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser);
    void DeactivateProjectSubsystemRole(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser);
}
