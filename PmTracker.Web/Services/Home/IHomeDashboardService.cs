using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Home;

public interface IHomeDashboardService
{
    DashboardPageViewModel BuildDashboard(CurrentUserContextViewModel currentUser);
}
