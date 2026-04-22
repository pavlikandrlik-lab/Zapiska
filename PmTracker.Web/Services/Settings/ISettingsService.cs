using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Settings;

public interface ISettingsService
{
    Task<NastaveniDashboardViewModel> BuildNastaveniDashboardAsync(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId, CancellationToken ct = default);
    Task<NastaveniPanelViewModel> BuildNastaveniPanelAsync(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId, CancellationToken ct = default);
    Task SaveUserRoleAssignmentAsync(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveUserRolesForUserAsync(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}
