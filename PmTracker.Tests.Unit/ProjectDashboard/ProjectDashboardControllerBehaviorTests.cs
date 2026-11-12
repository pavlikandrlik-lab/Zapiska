using PmTracker.Web.Services.ServiceDesk.Prodleni;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.ProjectDashboard;

public sealed class ProjectDashboardControllerBehaviorTests
{
    private const int AccessibleProjectId = 1;
    private const int InaccessibleProjectId = 99;

    private sealed class NullDatasetLoader : IZakladniDatasetLoader
    {
        public System.Threading.Tasks.Task<ZakladniDataset> LoadAsync(int projektId, Obdobi obdobi, System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult(new ZakladniDataset { Obdobi = obdobi });
    }

    [Fact]
    public async Task Index_ShouldReturnView_WhenUserHasAccess()
    {
        var controller = CreateController(
            new FakeProjectDashboardService(),
            isSuperAdmin: true,
            visibleProjectIds: [AccessibleProjectId]);

        var result = await controller.Index(AccessibleProjectId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeOfType<ProjectDashboardPageViewModel>();
    }

    /// <summary>
    /// Po D5 migraci je permission:dashboard.view enforcement na [Authorize(Policy)] atributu,
    /// ne v těle akce. Tento unit test ověřuje, že akce vrátí View pokud
    /// CanAccessProject projde (projektId je viditelný) — permission policy je ověřena
    /// middleware před voláním akce (testováno v PermissionPolicyHandlerTests).
    /// </summary>
    [Fact]
    public async Task Index_ShouldReturnView_WhenUserCanAccessProject()
    {
        var service = new FakeProjectDashboardService(canAccess: true);
        var controller = CreateController(
            service,
            isSuperAdmin: false,
            visibleProjectIds: [AccessibleProjectId]);

        var result = await controller.Index(AccessibleProjectId);

        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task RecordsPanel_ShouldReturnPartialView()
    {
        var controller = CreateController(
            new FakeProjectDashboardService(),
            isSuperAdmin: true,
            visibleProjectIds: [AccessibleProjectId]);

        var result = await controller.RecordsPanel(AccessibleProjectId);

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        partial.ViewName.Should().Be("~/Views/ProjectDashboard/_RecordsPanel.cshtml");
        partial.Model.Should().BeOfType<ProjectDashboardRecordsPanelViewModel>();
    }

    [Fact]
    public async Task StatisticsPanel_ShouldReturnPartialView()
    {
        var controller = CreateController(
            new FakeProjectDashboardService(),
            isSuperAdmin: true,
            visibleProjectIds: [AccessibleProjectId]);

        var result = await controller.StatisticsPanel(AccessibleProjectId);

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        partial.ViewName.Should().Be("~/Views/ProjectDashboard/Zakladni/Report.cshtml");
        partial.Model.Should().BeOfType<ZakladniReportViewModel>().Which.Obdobi.Druh.Should().Be(ObdobiDruh.Rok);
    }

    [Fact]
    public async Task StatisticsPanel_kvartal_a_rozsah()
    {
        var controller = CreateController(new FakeProjectDashboardService(), isSuperAdmin: true, visibleProjectIds: [AccessibleProjectId]);

        var kvartal = await controller.StatisticsPanel(AccessibleProjectId, rok: 2025, kvartal: 2);
        ((ZakladniReportViewModel)((PartialViewResult)kvartal).Model!).Obdobi.Should().Be(Obdobi.Kvartal(2025, 2));

        var rozsah = await controller.StatisticsPanel(AccessibleProjectId, od: new DateTime(2025, 3, 1), doDate: new DateTime(2025, 3, 31));
        ((ZakladniReportViewModel)((PartialViewResult)rozsah).Model!).Obdobi.Druh.Should().Be(ObdobiDruh.Rozsah);

        (await controller.StatisticsPanel(AccessibleProjectId, rok: 2025, kvartal: 5)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.StatisticsPanel(AccessibleProjectId, od: new DateTime(2025, 4, 1), doDate: new DateTime(2025, 3, 1)))
            .Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task NesPanel_ShouldReturnPlaceholderPartialView()
    {
        var controller = CreateController(
            new FakeProjectDashboardService(),
            isSuperAdmin: true,
            visibleProjectIds: [AccessibleProjectId]);

        var result = await controller.NesPanel(AccessibleProjectId);

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        partial.ViewName.Should().Be("~/Views/ProjectDashboard/_NesPanel.cshtml");
        var model = partial.Model.Should().BeOfType<ProjectDashboardNesPanelViewModel>().Subject;
        model.Prehled.Stav.Should().Be(ProdleniStavDat.BezNapojeni);
    }

    private static ProjectDashboardController CreateController(
        IProjectDashboardService service,
        bool isSuperAdmin,
        IReadOnlyList<int> visibleProjectIds)
    {
        var controller = new ProjectDashboardController(
            new FakeUserContextResolver(),
            TimeProvider.System,
            NullLoggerFactory.Instance,
            service,
            new PmTracker.Web.Services.Export.NesPanelExcelExportService(),
            new ZakladniReportBuilder(new NullDatasetLoader(), System.Array.Empty<IZakladniChartProvider>()),
            new DashboardSouhrnService(service, new Moq.Mock<PmTracker.Web.Services.ServiceDesk.Prodleni.IProdleniService>().Object))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        SetCurrentUserContext(controller, BuildCurrentUserContext(isSuperAdmin, visibleProjectIds));
        // Index nastavuje drobečky (SetProjectBreadcrumbs → Url.Action) → IUrlHelper musí být přítomen.
        controller.Url = new StubUrlHelper();
        return controller;
    }

    private sealed class StubUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string? Action(UrlActionContext actionContext) => "/stub";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/stub";
        public string? RouteUrl(UrlRouteContext routeContext) => "/stub";
    }

    private static void SetCurrentUserContext(
        ProjectDashboardController controller,
        CurrentUserContextViewModel userContext)
    {
        var field = typeof(BaseController).GetField(
            "<CurrentUserContext>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(controller, userContext);
    }

    private static CurrentUserContextViewModel BuildCurrentUserContext(
        bool isSuperAdmin,
        IReadOnlyList<int> visibleProjectIds)
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
            IsSuperAdmin = isSuperAdmin,
            RoleKody = [],
            VisibleProjectIds = visibleProjectIds,
            DeletedProjectIds = [],
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: isSuperAdmin,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };
    }

    private sealed class FakeProjectDashboardService : IProjectDashboardService
    {
        private readonly bool _canAccess;

        public FakeProjectDashboardService(bool canAccess = true)
        {
            _canAccess = canAccess;
        }

        public Task<ProjectDashboardPageViewModel> BuildDashboardPageAsync(int projectId, CancellationToken ct)
        {
            return Task.FromResult(new ProjectDashboardPageViewModel
            {
                Projekt = new ProjektHeaderViewModel
                {
                    Id = projectId,
                    Nazev = "Test Projekt",
                    Zkratka = "TP",
                    Stav = "Bezi"
                },
                RecordsPanelUrl = $"/projekty/{projectId}/dashboard/records-panel",
                NesPanelUrl = $"/projekty/{projectId}/dashboard/nes-panel",
                StatisticsPanelUrl = $"/projekty/{projectId}/dashboard/statistics-panel",
                SouhrnUrl = $"/projekty/{projectId}/dashboard/souhrn",
                BackUrl = $"/projekty/detail/{projectId}"
            });
        }

        public Task<ProjectDashboardRecordsPanelViewModel> BuildRecordsPanelAsync(
            int projectId,
            DateTime referenceDate,
            CancellationToken ct)
        {
            return Task.FromResult(new ProjectDashboardRecordsPanelViewModel
            {
                Records = []
            });
        }

        public Task<ProjectDashboardNesPanelViewModel> BuildNesPanelAsync(
            int projektId, DateTime reference, CancellationToken ct = default)
        {
            return Task.FromResult(new ProjectDashboardNesPanelViewModel
            {
                Prehled = ProdleniPrehled.Prazdny(ProdleniStavDat.BezNapojeni, null, null),
                ProjektId = projektId,
                Nyni = reference
            });
        }

        // CanAccessDashboardAsync smazáno v redesignu 2026-04-23.
    }

    private sealed class FakeUserContextResolver : IUserContextResolver
    {
        public Task<UserContextResolutionResult> ResolveAsync(HttpContext httpContext, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }
}
