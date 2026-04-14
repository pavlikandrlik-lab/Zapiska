using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Dashboard;

public interface IDashboardService
{
    DashboardPageViewModel BuildDashboardPage(CurrentUserContextViewModel currentUser);
    Task<DashboardFocusPanelViewModel> BuildFocusPanelAsync(CurrentUserContextViewModel currentUser, int limit, CancellationToken ct);
    Task<DashboardMeetingsPanelViewModel> BuildMeetingsPanelAsync(CurrentUserContextViewModel currentUser, int limit, CancellationToken ct);
    Task<DashboardNewsPanelViewModel> BuildNewsPanelAsync(CurrentUserContextViewModel currentUser, int take, CancellationToken ct);
    Task<DashboardFocusListPageViewModel> BuildFocusListPageAsync(CurrentUserContextViewModel currentUser, CancellationToken ct);
    Task<DashboardMeetingsListPageViewModel> BuildMeetingsListPageAsync(CurrentUserContextViewModel currentUser, CancellationToken ct);
    Task<DashboardNewsListPageViewModel> BuildNewsListPageAsync(CurrentUserContextViewModel currentUser, int take, CancellationToken ct);
}
