using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Settings;

namespace PmTracker.Web.Services.Settings;

public sealed class SettingsService(
    ISettingsAuthzQueries queries,
    ISettingsAuthzCommands commands) : ISettingsService
{
    public NastaveniDashboardViewModel BuildNastaveniDashboard(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId) =>
        queries.BuildNastaveniDashboard(section, currentUser, userId, projektId);

    public NastaveniPanelViewModel BuildNastaveniPanel(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId) =>
        queries.BuildNastaveniPanel(section, currentUser, userId, projektId);

    public void SaveAuthzRole(SaveAuthzRoleCommand command, CurrentUserContextViewModel currentUser) =>
        commands.SaveAuthzRole(command, currentUser);

    public void ToggleAuthzRole(ToggleAuthzRoleCommand command, CurrentUserContextViewModel currentUser) =>
        commands.ToggleAuthzRole(command, currentUser);

    public void SaveAuthzPermission(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser) =>
        commands.SaveAuthzPermission(command, currentUser);

    public void ToggleAuthzPermission(ToggleAuthzPermissionCommand command, CurrentUserContextViewModel currentUser) =>
        commands.ToggleAuthzPermission(command, currentUser);

    public void SaveUserRoleAssignment(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser) =>
        commands.SaveUserRoleAssignment(command, currentUser);

    public void SaveUserRolesForUser(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser) =>
        commands.SaveUserRolesForUser(command, currentUser);

    public void SaveRolePermission(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser) =>
        commands.SaveRolePermission(command, currentUser);

    public void DeleteRolePermission(DeleteRolePermissionCommand command, CurrentUserContextViewModel currentUser) =>
        commands.DeleteRolePermission(command, currentUser);
}
