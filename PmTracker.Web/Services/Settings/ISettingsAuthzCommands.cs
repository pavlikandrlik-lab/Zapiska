using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Settings;

public interface ISettingsAuthzCommands
{
    Task SaveUserRoleAssignmentAsync(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveUserRolesForUserAsync(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}
