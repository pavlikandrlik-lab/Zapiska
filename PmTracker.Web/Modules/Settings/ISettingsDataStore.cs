using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Settings;

public interface ISettingsDataStore
{
    NastaveniDashboardViewModel BuildNastaveniDashboard(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId);
    NastaveniPanelViewModel BuildNastaveniPanel(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId);
    void SaveAuthzRole(SaveAuthzRoleCommand command, CurrentUserContextViewModel currentUser);
    void ToggleAuthzRole(ToggleAuthzRoleCommand command, CurrentUserContextViewModel currentUser);
    void SaveAuthzPermission(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser);
    void ToggleAuthzPermission(ToggleAuthzPermissionCommand command, CurrentUserContextViewModel currentUser);
    void SaveUserRoleAssignment(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser);
    void SaveUserRolesForUser(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser);
    void SaveRolePermission(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser);
    void DeleteRolePermission(DeleteRolePermissionCommand command, CurrentUserContextViewModel currentUser);
}
