using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Projects.Commands;

namespace PmTracker.Web.Modules.Projects;

public sealed class ProjectsCommands(
    ISaveProjectCommandHandler saveProjectCommandHandler,
    ISoftDeleteProjectCommandHandler softDeleteProjectCommandHandler,
    ISaveTeamMemberCommandHandler saveTeamMemberCommandHandler,
    IRemoveTeamMemberCommandHandler removeTeamMemberCommandHandler,
    IAssignProjectRoleCommandHandler assignProjectRoleCommandHandler,
    IDeactivateProjectRoleCommandHandler deactivateProjectRoleCommandHandler,
    IAssignProjectSubsystemCommandHandler assignProjectSubsystemCommandHandler,
    IDeactivateProjectSubsystemCommandHandler deactivateProjectSubsystemCommandHandler,
    IAssignProjectSubsystemRoleCommandHandler assignProjectSubsystemRoleCommandHandler,
    IDeactivateProjectSubsystemRoleCommandHandler deactivateProjectSubsystemRoleCommandHandler) : IProjectsCommands
{
    public int SaveProject(SaveProjectCommand command, CurrentUserContextViewModel currentUser)
        => saveProjectCommandHandler.Handle(command, currentUser);

    public void SoftDeleteProject(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser)
        => softDeleteProjectCommandHandler.Handle(command, currentUser);

    public void SaveTeamMember(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser)
        => saveTeamMemberCommandHandler.Handle(command, currentUser);

    public void RemoveTeamMember(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser)
        => removeTeamMemberCommandHandler.Handle(command, currentUser);

    public void AssignProjectRole(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser)
        => assignProjectRoleCommandHandler.Handle(command, currentUser);

    public void DeactivateProjectRole(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser)
        => deactivateProjectRoleCommandHandler.Handle(command, currentUser);

    public void AssignProjectSubsystem(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser)
        => assignProjectSubsystemCommandHandler.Handle(command, currentUser);

    public void DeactivateProjectSubsystem(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser)
        => deactivateProjectSubsystemCommandHandler.Handle(command, currentUser);

    public void AssignProjectSubsystemRole(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser)
        => assignProjectSubsystemRoleCommandHandler.Handle(command, currentUser);

    public void DeactivateProjectSubsystemRole(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser)
        => deactivateProjectSubsystemRoleCommandHandler.Handle(command, currentUser);
}
