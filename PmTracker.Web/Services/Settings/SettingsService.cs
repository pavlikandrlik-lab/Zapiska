using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Settings;

public sealed class SettingsService : ISettingsService
{
    private readonly IPmTrackerDataStore _dataStore;

    public SettingsService(IPmTrackerDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public NastaveniDashboardViewModel BuildNastaveniDashboard(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId) =>
        _dataStore.BuildNastaveniDashboard(section, currentUser, userId, projektId);

    public NastaveniPanelViewModel BuildNastaveniPanel(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId) =>
        _dataStore.BuildNastaveniPanel(section, currentUser, userId, projektId);

    public void SaveAuthzRole(SaveAuthzRoleCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.SaveAuthzRole(command, currentUser);

    public void ToggleAuthzRole(ToggleAuthzRoleCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.ToggleAuthzRole(command, currentUser);

    public void SaveAuthzPermission(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.SaveAuthzPermission(command, currentUser);

    public void ToggleAuthzPermission(ToggleAuthzPermissionCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.ToggleAuthzPermission(command, currentUser);

    public void SaveUserRoleAssignment(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.SaveUserRoleAssignment(command, currentUser);

    public void SaveUserRolesForUser(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.SaveUserRolesForUser(command, currentUser);

    public void SaveRolePermission(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser) =>
        _dataStore.SaveRolePermission(command, currentUser);
}
