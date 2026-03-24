using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Settings;

public interface ISettingsAuthzCommands
{
    Task SaveAuthzRoleAsync(SaveAuthzRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task ToggleAuthzRoleAsync(ToggleAuthzRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveAuthzPermissionAsync(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task ToggleAuthzPermissionAsync(ToggleAuthzPermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveUserRoleAssignmentAsync(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveUserRolesForUserAsync(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveRolePermissionAsync(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeleteRolePermissionAsync(DeleteRolePermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}
