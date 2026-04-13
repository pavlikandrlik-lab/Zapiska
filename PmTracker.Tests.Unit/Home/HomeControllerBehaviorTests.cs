using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Home;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Home;

public sealed class HomeControllerBehaviorTests
{
    [Fact]
    public void Index_ShouldRenderDashboardView()
    {
        var dashboard = new DashboardPageViewModel
        {
            PageTitle = "Přehled",
            Subtitle = "Výchozí rozcestník",
            Hero = new DashboardHeroViewModel
            {
                Eyebrow = "Dashboard",
                Title = "Dobrý den",
                Description = "Popis",
                Stats = []
            },
            Sections = []
        };
        var controller = CreateController(new FakeHomeDashboardService { Dashboard = dashboard });

        var result = controller.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeSameAs(dashboard);
    }

    private static HomeController CreateController(IHomeDashboardService dashboardService)
    {
        var controller = new HomeController(
            new FakeUserContextResolver(),
            TimeProvider.System,
            NullLoggerFactory.Instance,
            dashboardService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        SetCurrentUserContext(controller, BuildCurrentUserContext());
        return controller;
    }

    private static void SetCurrentUserContext(HomeController controller, CurrentUserContextViewModel userContext)
    {
        var field = typeof(BaseController).GetField("<CurrentUserContext>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(controller, userContext);
    }

    private static CurrentUserContextViewModel BuildCurrentUserContext()
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 1,
            Jmeno = "Jan",
            Prijmeni = "Tester",
            DisplayName = "Jan Tester",
            Email = "jan@test.local",
            OrganizacniCelek = "QA",
            OrganizacniCelekKod = "QA",
            IsSuperAdmin = false,
            RoleKody = [],
            VisibleProjectIds = [1],
            DeletedProjectIds = [],
            PermissionGrants = []
        };
    }

    private sealed class FakeHomeDashboardService : IHomeDashboardService
    {
        public DashboardPageViewModel Dashboard { get; set; } = null!;

        public DashboardPageViewModel BuildDashboard(CurrentUserContextViewModel currentUser)
        {
            return Dashboard;
        }
    }

    private sealed class FakeUserContextResolver : IUserContextResolver
    {
        public Task<UserContextResolutionResult> ResolveAsync(HttpContext httpContext, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }
}
