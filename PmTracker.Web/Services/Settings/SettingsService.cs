using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Settings;

public sealed class SettingsService(
    ISettingsAuthzQueries queries,
    ISettingsAuthzCommands commands) : ISettingsService
{
    public Task<NastaveniDashboardViewModel> BuildNastaveniDashboardAsync(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId, CancellationToken ct = default) =>
        queries.BuildNastaveniDashboardAsync(section, currentUser, userId, projektId, ct);

    public Task<NastaveniPanelViewModel> BuildNastaveniPanelAsync(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId, CancellationToken ct = default) =>
        queries.BuildNastaveniPanelAsync(section, currentUser, userId, projektId, ct);

    public Task SaveAuthzRoleAsync(SaveAuthzRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) =>
        commands.SaveAuthzRoleAsync(command, currentUser, ct);

    public Task ToggleAuthzRoleAsync(ToggleAuthzRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) =>
        commands.ToggleAuthzRoleAsync(command, currentUser, ct);

    public Task SaveAuthzPermissionAsync(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) =>
        commands.SaveAuthzPermissionAsync(command, currentUser, ct);

    public Task ToggleAuthzPermissionAsync(ToggleAuthzPermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) =>
        commands.ToggleAuthzPermissionAsync(command, currentUser, ct);

    public Task SaveUserRoleAssignmentAsync(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) =>
        commands.SaveUserRoleAssignmentAsync(command, currentUser, ct);

    public Task SaveUserRolesForUserAsync(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) =>
        commands.SaveUserRolesForUserAsync(command, currentUser, ct);

    public Task SaveRolePermissionAsync(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) =>
        commands.SaveRolePermissionAsync(command, currentUser, ct);

    public Task DeleteRolePermissionAsync(DeleteRolePermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) =>
        commands.DeleteRolePermissionAsync(command, currentUser, ct);
}
