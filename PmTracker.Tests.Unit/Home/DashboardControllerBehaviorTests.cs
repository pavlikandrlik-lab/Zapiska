using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Dashboard;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Home;

public sealed class DashboardControllerBehaviorTests
{
    [Fact]
    public void Index_ShouldRenderDashboardShell()
    {
        var controller = CreateController(new FakeDashboardService());

        var result = controller.Index();

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeOfType<DashboardPageViewModel>();
    }

    [Fact]
    public async Task FocusPanel_ShouldReturnPartialView()
    {
        var controller = CreateController(new FakeDashboardService());

        var result = await controller.FocusPanel();

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        partial.ViewName.Should().Be("~/Views/Dashboard/_DashboardFocusPanel.cshtml");
        partial.Model.Should().BeOfType<DashboardFocusPanelViewModel>();
    }

    private static DashboardController CreateController(IDashboardService service)
    {
        var controller = new DashboardController(
            new FakeUserContextResolver(),
            TimeProvider.System,
            NullLoggerFactory.Instance,
            service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        SetCurrentUserContext(controller, BuildCurrentUserContext());
        return controller;
    }

    private static void SetCurrentUserContext(DashboardController controller, CurrentUserContextViewModel userContext)
    {
        var field = typeof(BaseController).GetField("<CurrentUserContext>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
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

    private sealed class FakeDashboardService : IDashboardService
    {
        public DashboardPageViewModel BuildDashboardPage(CurrentUserContextViewModel currentUser)
        {
            return new DashboardPageViewModel
            {
                CurrentUserContext = currentUser,
                PageTitle = "Přehled",
                Subtitle = "Dashboard",
                FocusPanelUrl = "/dashboard/focus-panel",
                MeetingsPanelUrl = "/dashboard/meetings-panel",
                NewsPanelUrl = "/dashboard/news-panel"
            };
        }

        public Task<DashboardFocusPanelViewModel> BuildFocusPanelAsync(CurrentUserContextViewModel currentUser, int limit, CancellationToken ct)
        {
            return Task.FromResult(new DashboardFocusPanelViewModel
            {
                TotalCount = 0,
                ListUrl = "/dashboard/focus",
                Items = []
            });
        }

        public Task<DashboardMeetingsPanelViewModel> BuildMeetingsPanelAsync(CurrentUserContextViewModel currentUser, int limit, CancellationToken ct)
        {
            return Task.FromResult(new DashboardMeetingsPanelViewModel
            {
                TotalCount = 0,
                ListUrl = "/dashboard/meetings",
                Items = []
            });
        }

        public Task<DashboardNewsPanelViewModel> BuildNewsPanelAsync(CurrentUserContextViewModel currentUser, int take, CancellationToken ct)
        {
            return Task.FromResult(new DashboardNewsPanelViewModel
            {
                LoadedCount = 0,
                TotalCount = 0,
                CanLoadMore = false,
                ListUrl = "/dashboard/news",
                Items = []
            });
        }

        public Task<DashboardFocusListPageViewModel> BuildFocusListPageAsync(CurrentUserContextViewModel currentUser, CancellationToken ct)
        {
            return Task.FromResult(new DashboardFocusListPageViewModel
            {
                CurrentUserContext = currentUser,
                PageTitle = "Focus",
                Subtitle = "Focus",
                Items = []
            });
        }

        public Task<DashboardMeetingsListPageViewModel> BuildMeetingsListPageAsync(CurrentUserContextViewModel currentUser, CancellationToken ct)
        {
            return Task.FromResult(new DashboardMeetingsListPageViewModel
            {
                CurrentUserContext = currentUser,
                PageTitle = "Meetings",
                Subtitle = "Meetings",
                Items = []
            });
        }

        public Task<DashboardNewsListPageViewModel> BuildNewsListPageAsync(CurrentUserContextViewModel currentUser, int take, CancellationToken ct)
        {
            return Task.FromResult(new DashboardNewsListPageViewModel
            {
                CurrentUserContext = currentUser,
                PageTitle = "News",
                Subtitle = "News",
                Items = []
            });
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
