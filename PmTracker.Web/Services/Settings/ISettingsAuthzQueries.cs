using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Settings;

public interface ISettingsAuthzQueries
{
    Task<NastaveniDashboardViewModel> BuildNastaveniDashboardAsync(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId, CancellationToken ct = default);
    Task<NastaveniPanelViewModel> BuildNastaveniPanelAsync(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId, CancellationToken ct = default);
}
