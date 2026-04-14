using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Home;

public sealed class HomeDashboardService : IHomeDashboardService
{
    public DashboardPageViewModel BuildDashboard(CurrentUserContextViewModel currentUser)
    {
        return new DashboardPageViewModel
        {
            CurrentUserContext = currentUser,
            PageTitle = "Přehled",
            Subtitle = "Výchozí rozcestník pro každodenní práci v aplikaci.",
            FocusPanelUrl = "/dashboard/focus-panel",
            MeetingsPanelUrl = "/dashboard/meetings-panel",
            NewsPanelUrl = "/dashboard/news-panel"
        };
    }
}
