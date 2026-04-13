using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Home;

namespace PmTracker.Tests.Unit.Home;

public sealed class HomeDashboardServiceTests
{
    [Fact]
    public void BuildDashboard_ShouldIncludePermissionBasedCards()
    {
        var service = new HomeDashboardService();
        var currentUser = BuildContext(
            PermissionKeys.PeopleManage,
            PermissionKeys.CiselnikyEdit,
            PermissionKeys.SettingsView);

        var model = service.BuildDashboard(currentUser);

        model.PageTitle.Should().Be("Přehled");
        var quickAccess = model.Sections.Should().ContainSingle(x => x.Key == "quick-access").Subject;
        quickAccess.Cards.Should().Contain(x => x.Controller == "Osoby");
        quickAccess.Cards.Should().Contain(x => x.Controller == "Ciselniky");
        quickAccess.Cards.Should().Contain(x => x.Controller == "Nastaveni");
    }

    [Fact]
    public void BuildDashboard_ShouldKeepPlaceholderSectionsSeparated_FromQuickAccess()
    {
        var service = new HomeDashboardService();

        var model = service.BuildDashboard(BuildContext());

        model.Sections.Should().HaveCount(3);
        model.Sections[1].Cards.Should().OnlyContain(x => x.IsPlaceholder);
        model.Sections[2].Cards.Should().OnlyContain(x => x.IsPlaceholder);
        model.Sections[0].Cards.Should().Contain(x => x.Controller == "Projekty" && x.IsPrimary);
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
