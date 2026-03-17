using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IProjectAssignmentCommandsUseCase
{
    void AssignProjectRole(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser);

    void DeactivateProjectRole(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser);

    void AssignProjectSubsystem(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser);

    void DeactivateProjectSubsystem(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser);

    void AssignProjectSubsystemRole(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser);

    void DeactivateProjectSubsystemRole(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser);
}
