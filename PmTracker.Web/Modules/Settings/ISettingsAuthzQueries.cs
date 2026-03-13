using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Settings;

public interface ISettingsAuthzQueries
{
    NastaveniDashboardViewModel BuildNastaveniDashboard(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId);
    NastaveniPanelViewModel BuildNastaveniPanel(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId);
}
