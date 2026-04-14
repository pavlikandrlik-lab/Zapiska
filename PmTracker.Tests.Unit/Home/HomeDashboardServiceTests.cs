using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Home;

namespace PmTracker.Tests.Unit.Home;

public sealed class HomeDashboardServiceTests
{
    [Fact]
    public void BuildDashboard_ShouldReturnDashboardShellUrls()
    {
        var service = new HomeDashboardService();
        var currentUser = BuildContext();

        var model = service.BuildDashboard(currentUser);

        model.PageTitle.Should().Be("Přehled");
        model.FocusPanelUrl.Should().Be("/dashboard/focus-panel");
        model.MeetingsPanelUrl.Should().Be("/dashboard/meetings-panel");
        model.NewsPanelUrl.Should().Be("/dashboard/news-panel");
    }

    private static CurrentUserContextViewModel BuildContext(params string[] permissionKeys)
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 1,
            Jmeno = "Jan",
            Prijmeni = "Tester",
            DisplayName = "Jan Tester",
            Email = "jan.tester@test.local",
            OrganizacniCelek = "QA",
            OrganizacniCelekKod = "QA",
            IsSuperAdmin = false,
            RoleKody = ["USER"],
            VisibleProjectIds = [10, 11],
            DeletedProjectIds = [],
            PermissionGrants = permissionKeys
                .Select(permissionKey => new PermissionGrantViewModel
                {
                    PermissionKey = permissionKey,
                    ScopeLevel = "GLOBAL",
                    ScopeMode = "ALL",
                    IsAllowed = true,
                    ProjectIds = []
                })
                .ToList()
        };
    }
}
