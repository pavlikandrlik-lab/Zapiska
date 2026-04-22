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

    public Task SaveUserRoleAssignmentAsync(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) =>
        commands.SaveUserRoleAssignmentAsync(command, currentUser, ct);

    public Task SaveUserRolesForUserAsync(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) =>
        commands.SaveUserRolesForUserAsync(command, currentUser, ct);
}
