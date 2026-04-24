using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Projects;

public sealed class ProjektyControllerBehaviorTests
{
    [Fact]
    public async Task EditProjectModal_ShouldSelectStatusByLabel_WhenCodeDoesNotExistInOptions()
    {
        const int projectId = 42;
        var projectService = new FakeProjectService
        {
            ProjectStatusOptions =
            [
                new LookupOptionViewModel { Value = "RUN", Label = "Bezi" },
                new LookupOptionViewModel { Value = "PLAN", Label = "Planovano" }
            ],
            ProjektyList =
            [
                new ProjektListItemViewModel
                {
                    Id = projectId,
                    Zkratka = "PRJ",
                    Nazev = "Projekt fallback",
                    StavKod = "LEGACY_STATUS",
                    Stav = "Planovano",
                    CanEdit = true,
                    CanDelete = true
                }
            ],
            ProjektDetail = CreateEmptyProjektDetail(projectId)
        };
        var controller = CreateController(projectService);

        var result = await controller.EditProjectModal(projectId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("ProjectModal");
        var model = view.Model.Should().BeOfType<ProjectModalViewModel>().Subject;
        model.Command.Stav.Should().Be("PLAN");
    }

    [Fact]
    public async Task EditProjectModal_ShouldFallbackToFirstStatus_WhenCodeAndLabelDoNotMatch()
    {
        const int projectId = 43;
        var projectService = new FakeProjectService
        {
            ProjectStatusOptions =
            [
                new LookupOptionViewModel { Value = "RUN", Label = "Bezi" },
                new LookupOptionViewModel { Value = "PLAN", Label = "Planovano" }
            ],
            ProjektyList =
            [
                new ProjektListItemViewModel
                {
                    Id = projectId,
                    Zkratka = "PRJ2",
                    Nazev = "Projekt fallback first",
                    StavKod = "UNKNOWN_CODE",
                    Stav = "Neexistujici stav",
                    CanEdit = true,
                    CanDelete = true
                }
            ],
            ProjektDetail = CreateEmptyProjektDetail(projectId)
        };
        var controller = CreateController(projectService);

        var result = await controller.EditProjectModal(projectId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ProjectModalViewModel>().Subject;
        model.Command.Stav.Should().Be("RUN");
    }

    // NewMeetingModal test přesunut do JednaniControllerBehaviorTests po reorg
    // endpointů Jednání (viz docs/known-issues/meetings-endpoints-split-between-controllers.md, 2026-04-23).

    [Fact]
    public async Task RecordMeetingCommentStates_ShouldReturnJsonPayload_FromProjectService()
    {
        const int projectId = 77;
        var projectService = new FakeProjectService
        {
            ProjektDetail = CreateEmptyProjektDetail(projectId),
            RecordMeetingCommentStates =
                new Dictionary<int, IReadOnlyList<string>>
                {
                    [12] = ["1", "4"],
                    [19] = ["2"]
                }
        };
        var controller = CreateController(projectService);

        var result = await controller.RecordMeetingCommentStates(projectId);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var payload = json.Value.Should().BeOfType<ProjektMeetingCommentStatesResponseViewModel>().Subject;
        payload.StatesByRecordId.Should().ContainKey("12");
        payload.StatesByRecordId["12"].Should().BeEquivalentTo(["1", "4"]);
        payload.StatesByRecordId.Should().ContainKey("19");
        payload.StatesByRecordId["19"].Should().BeEquivalentTo(["2"]);
    }

    [Fact]
    public async Task SearchProjectMemberCandidates_ShouldReturnJsonPayload_FromProjectService()
    {
        const int projectId = 91;
        var projectService = new FakeProjectService
        {
            ProjektDetail = CreateEmptyProjektDetail(projectId),
            ProjectMemberSearchResults =
            [
                new PersonPickerEntryViewModel
                {
                    Id = 5,
                    Label = "Jan Novak",
                    Email = "jan.novak@test.local",
                    Organizace = "FIS",
                    OrganizacniCelek = "IT"
                }
            ]
        };
        var controller = CreateController(projectService);

        var result = await controller.SearchProjectMemberCandidates(projectId, "jan");

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var payload = json.Value.Should().BeOfType<PersonPickerSearchResponseViewModel>().Subject;
        payload.Results.Should().HaveCount(1);
        payload.Results[0].Id.Should().Be(5);
        payload.Results[0].Label.Should().Be("Jan Novak");
    }

    [Fact]
    public async Task JednaniTabPartial_ShouldUseCurrentYear_AsPreviewYear_WhenAvailable()
    {
        const int projectId = 118;
        var fixedUtcNow = new DateTimeOffset(2026, 4, 9, 10, 0, 0, TimeSpan.Zero);
        var currentYear = TimeZoneInfo.ConvertTime(fixedUtcNow, TimeZoneInfo.Local).Year;
        var projectService = new FakeProjectService
        {
            ProjektDetail = CreateEmptyProjektDetail(projectId),
            MeetingsTab = new ProjektJednaniTabViewModel
            {
                ProjektId = projectId,
                RocniSkupiny =
                [
                    new JednaniYearGroupViewModel { Rok = currentYear - 1, Jednani = [] },
                    new JednaniYearGroupViewModel { Rok = currentYear, Jednani = [] }
                ]
            }
        };
        var controller = CreateController(projectService, new FixedTimeProvider(fixedUtcNow));

        var result = await controller.JednaniTabPartial(projectId);

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        var model = partial.Model.Should().BeOfType<ProjektJednaniTabViewModel>().Subject;
        model.PreviewRok.Should().Be(currentYear);
    }

    [Fact]
    public async Task JednaniTabPartial_ShouldFallbackPreviewYear_ToNewestAvailableYear_WhenCurrentYearMissing()
    {
        const int projectId = 119;
        var fixedUtcNow = new DateTimeOffset(2026, 4, 9, 10, 0, 0, TimeSpan.Zero);
        var projectService = new FakeProjectService
        {
            ProjektDetail = CreateEmptyProjektDetail(projectId),
            MeetingsTab = new ProjektJednaniTabViewModel
            {
                ProjektId = projectId,
                RocniSkupiny =
                [
                    new JednaniYearGroupViewModel { Rok = 2024, Jednani = [] },
                    new JednaniYearGroupViewModel { Rok = 2025, Jednani = [] }
                ]
            }
        };
        var controller = CreateController(projectService, new FixedTimeProvider(fixedUtcNow));

        var result = await controller.JednaniTabPartial(projectId);

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        var model = partial.Model.Should().BeOfType<ProjektJednaniTabViewModel>().Subject;
        model.PreviewRok.Should().Be(2025);
    }

    [Fact]
    public void TeamManage_Actions_ShouldDoPerProjectCheck_InBody()
    {
        // Per-action redesign 2026-04-23: team.* akce provádějí per-project kontrolu v body
        // (ExecuteTeamValidatedActionAsync / ExecuteTeamActionAsync s permissionKey parametrem).
        // Policy atribut nefunguje — projektId je ve form body, ne v route.
        var code = System.IO.File.ReadAllText(
            PmTracker.Tests.Unit.Architecture.ArchitectureTestBase.ResolvePath(
                "PmTracker.Web/Controllers/ProjektyController.Commands.cs"));

        code.Should().Contain("CurrentUserContext.HasPermission(permissionKey, projektId)",
            "team.* akce mají per-project check v helperu s per-action klíčem jako parametrem");
        code.Should().Contain("PermissionKeys.TeamSubsystemReorder",
            "ReorderProjectSubsystem musí použít specific klíč team.subsystem.reorder");
    }

    [Fact]
    public async Task ReorderProjectSubsystem_ShouldInvokeService_WhenCalled()
    {
        // Verifies that the action body delegates correctly to the service.
        // Policy-level enforcement is validated by architecture tests (ProjektyAuthzTests).
        const int projectId = 132;
        var projectService = new FakeProjectService
        {
            ProjektDetail = CreateEmptyProjektDetail(projectId)
        };
        var controller = CreateController(projectService);
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var command = new ReorderProjectSubsystemCommand
        {
            ProjektId = projectId,
            ProjektSubsystemId = 17,
            Direction = ProjectSubsystemReorderDirections.Down
        };

        var result = await controller.ReorderProjectSubsystem(command);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var payload = json.Value.Should().BeOfType<ModalSubmitResultViewModel>().Subject;
        payload.Ok.Should().BeTrue();
        payload.ProjectId.Should().Be(projectId);
        payload.Tab.Should().Be("tym");
        payload.RefreshScope.Should().Be("projekty-detail-tym");
        projectService.ReorderProjectSubsystemCallCount.Should().Be(1);
        projectService.LastReorderProjectSubsystemCommand.Should().NotBeNull();
        projectService.LastReorderProjectSubsystemCommand!.ProjektId.Should().Be(projectId);
        projectService.LastReorderProjectSubsystemCommand.ProjektSubsystemId.Should().Be(17);
        projectService.LastReorderProjectSubsystemCommand.Direction.Should().Be(ProjectSubsystemReorderDirections.Down);
    }

    private static ProjektyController CreateController(
        FakeProjectService projectService,
        TimeProvider? timeProvider = null,
        FakeMeetingService? meetingService = null,
        FakeRecordProposalService? recordProposalService = null)
    {
        var services = new ServiceCollection();
        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);
        }

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        var controller = new ProjektyController(
            userContextResolver: null!,
            timeProvider: timeProvider ?? TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            projectService: projectService,
            meetingService: meetingService ?? new FakeMeetingService(),
            recordProposalService: recordProposalService ?? new FakeRecordProposalService(),
            projectDashboardService: new FakeProjectDashboardService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        controller.Url = new StubUrlHelper();
        controller.TempData = new TempDataDictionary(httpContext, new StubTempDataProvider());
        SetCurrentUserContext(controller, BuildSuperAdminContext());
        return controller;
    }

    private static CurrentUserContextViewModel BuildSuperAdminContext()
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 1,
            Jmeno = "Unit",
            Prijmeni = "Tester",
            DisplayName = "Unit Tester",
            Email = "unit@test.local",
            OrganizacniCelek = "Test",
            OrganizacniCelekKod = "TEST",
            IsSuperAdmin = true,
            RoleKody = [],
            VisibleProjectIds = [],
            DeletedProjectIds = [],
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: true,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };
    }

    private static CurrentUserContextViewModel BuildProjectUserContext(int projectId, params string[] permissionKeys)
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 2,
            Jmeno = "Project",
            Prijmeni = "Tester",
            DisplayName = "Project Tester",
            Email = "project@test.local",
            OrganizacniCelek = "Test",
            OrganizacniCelekKod = "TEST",
            IsSuperAdmin = false,
            RoleKody = [],
            VisibleProjectIds = [projectId],
            DeletedProjectIds = [],
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: false,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
                {
                    {
                        projectId,
                        new HashSet<string>(permissionKeys, StringComparer.OrdinalIgnoreCase)
                    }
                },
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };
    }

    private static void SetCurrentUserContext(ProjektyController controller, CurrentUserContextViewModel userContext)
    {
        var field = typeof(BaseController).GetField("<CurrentUserContext>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull("BaseController musi mit backing field pro CurrentUserContext");
        field!.SetValue(controller, userContext);
    }

    private static ProjektDetailViewModel CreateEmptyProjektDetail(
        int projectId)
    {
        return new ProjektDetailViewModel
        {
            Projekt = new ProjektHeaderViewModel
            {
                Id = projectId,
                Nazev = "Projekt",
                Zkratka = "PRJ",
                Stav = "Bezi"
            },
            ZaznamyTab = new ProjektZaznamyTabViewModel
            {
                ProjektId = projectId,
                DleSubsystemu = true,
                Zaznamy = [],
                SkupinyZaznamu = [],
                Filtry = new ProjektFiltryViewModel
                {
                    Subsystemy = [],
                    SubsystemyMoznosti = [],
                    Kategorie = [],
                    KategorieMoznosti = [],
                    StavyUkolu = [],
                    StavyUkoluMoznosti = [],
                    TypyUkolu = [],
                    TypyUkoluMoznosti = [],
                    Vlastnici = [],
                    VlastniciMoznosti = [],
                    StavyJednaniVyjadreni = []
                }
            },
            HarmonogramTab = new ProjektLazyTabShellViewModel { TabKey = "harmonogram", LoadingText = "Načítání harmonogramu..." },
            JednaniTab = new ProjektLazyTabShellViewModel { TabKey = "jednani", LoadingText = "Načítání jednání..." },
            TymTab = new ProjektLazyTabShellViewModel { TabKey = "tym", LoadingText = "Načítání týmu..." }
        };
    }

    private sealed class FakeProjectService : IProjectService
    {
        public IReadOnlyList<ProjektListItemViewModel> ProjektyList { get; init; } = [];
        public IReadOnlyList<LookupOptionViewModel> ProjectStatusOptions { get; init; } = [];
        public ProjektDetailViewModel ProjektDetail { get; init; } = CreateEmptyProjektDetail(0);
        public ProjektZaznamyTabViewModel RecordsTab { get; init; } = new();
        public ProjektHarmonogramTabViewModel ScheduleTab { get; init; } = new();
        public ProjektJednaniTabViewModel MeetingsTab { get; init; } = new();
        public ProjektTymTabViewModel TeamTab { get; init; } = new();
        public ProjectTeamModalOptionsViewModel TeamModalOptions { get; init; } = new();
        public IReadOnlyDictionary<int, IReadOnlyList<string>> RecordMeetingCommentStates { get; init; } = new Dictionary<int, IReadOnlyList<string>>();
        public IReadOnlyList<PersonPickerEntryViewModel> ProjectMemberSearchResults { get; init; } = [];
        public int ReorderProjectSubsystemCallCount { get; private set; }
        public ReorderProjectSubsystemCommand? LastReorderProjectSubsystemCommand { get; private set; }

        public Task<bool> ProjektExistsAsync(int id, CancellationToken ct = default)
            => Task.FromResult(ProjektyList.Any(item => item.Id == id) || ProjektDetail.Projekt.Id == id);

        public Task<IReadOnlyList<ProjektListItemViewModel>> BuildProjektyListAsync(CancellationToken ct = default)
            => Task.FromResult(ProjektyList);

        public Task<ProjektListItemViewModel?> GetProjectListItemAsync(int id, CancellationToken ct = default)
            => Task.FromResult(ProjektyList.FirstOrDefault(item => item.Id == id));

        public Task<ProjektDetailViewModel> BuildProjektDetailAsync(int id, CancellationToken ct = default)
            => Task.FromResult(ProjektDetail);

        public Task<ProjektZaznamyTabViewModel> BuildProjectRecordsTabAsync(int id, CancellationToken ct = default)
            => Task.FromResult(new ProjektZaznamyTabViewModel
            {
                ProjektId = id,
                DleSubsystemu = RecordsTab.DleSubsystemu,
                Zaznamy = RecordsTab.Zaznamy,
                SkupinyZaznamu = RecordsTab.SkupinyZaznamu,
                Filtry = RecordsTab.Filtry,
                CurrentUserOsobaId = RecordsTab.CurrentUserOsobaId,
                CanManageRecords = RecordsTab.CanManageRecords,
                CreateRecordEditorUrl = RecordsTab.CreateRecordEditorUrl,
                RefreshUrl = RecordsTab.RefreshUrl,
                MeetingCommentStatesUrl = RecordsTab.MeetingCommentStatesUrl,
                ProjectPrintUrl = RecordsTab.ProjectPrintUrl,
                ProjectWordUrl = RecordsTab.ProjectWordUrl
            });

        public Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> BuildRecordMeetingCommentStatesAsync(int projectId, CancellationToken ct = default)
            => Task.FromResult(RecordMeetingCommentStates);

        public Task<ProjektHarmonogramTabViewModel> BuildProjectScheduleTabAsync(int id, CancellationToken ct = default)
            => Task.FromResult(new ProjektHarmonogramTabViewModel
            {
                ProjektId = id,
                HarmonogramUkoly = ScheduleTab.HarmonogramUkoly,
                SubsystemyMoznosti = ScheduleTab.SubsystemyMoznosti
            });

        public Task<ProjektJednaniTabViewModel> BuildProjectMeetingsTabAsync(int id, CancellationToken ct = default)
            => Task.FromResult(new ProjektJednaniTabViewModel
            {
                ProjektId = id,
                Jednani = MeetingsTab.Jednani,
                RocniSkupiny = MeetingsTab.RocniSkupiny,
                StavyJednani = MeetingsTab.StavyJednani
            });

        public Task<ProjektTymTabViewModel> BuildProjectTeamTabAsync(int id, CancellationToken ct = default)
            => Task.FromResult(new ProjektTymTabViewModel
            {
                ProjektId = id,
                AktivniRole = TeamTab.AktivniRole,
                HistorieRoli = TeamTab.HistorieRoli,
                AktivniSubsystemyProjektu = TeamTab.AktivniSubsystemyProjektu,
                DostupneOsobyProRole = TeamTab.DostupneOsobyProRole,
                DostupneProjektoveSubsystemy = TeamTab.DostupneProjektoveSubsystemy,
                RoleProjektu = TeamTab.RoleProjektu,
                RoleSubsystemu = TeamTab.RoleSubsystemu,
                DostupneSubsystemy = TeamTab.DostupneSubsystemy
            });

        public Task<ProjectTeamModalOptionsViewModel> BuildProjectTeamModalOptionsAsync(int id, CancellationToken ct = default)
            => Task.FromResult(new ProjectTeamModalOptionsViewModel
            {
                RoleProjektu = TeamModalOptions.RoleProjektu.Count == 0 ? TeamTab.RoleProjektu : TeamModalOptions.RoleProjektu,
                RoleSubsystemu = TeamModalOptions.RoleSubsystemu.Count == 0 ? TeamTab.RoleSubsystemu : TeamModalOptions.RoleSubsystemu,
                DostupneProjektoveSubsystemy = TeamModalOptions.DostupneProjektoveSubsystemy.Count == 0 ? TeamTab.DostupneProjektoveSubsystemy : TeamModalOptions.DostupneProjektoveSubsystemy,
                DostupneSubsystemy = TeamModalOptions.DostupneSubsystemy.Count == 0 ? TeamTab.DostupneSubsystemy : TeamModalOptions.DostupneSubsystemy
            });

        public Task ReorderProjectSubsystemAsync(ReorderProjectSubsystemCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        {
            ReorderProjectSubsystemCallCount++;
            LastReorderProjectSubsystemCommand = command;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PersonPickerEntryViewModel>> SearchProjectMemberCandidatesAsync(string query, CancellationToken ct = default)
            => Task.FromResult(ProjectMemberSearchResults);

        public Task<ProjektZaznamCardShellViewModel?> BuildRecordCardShellAsync(int projectId, int recordId, CancellationToken ct = default)
            => Task.FromResult<ProjektZaznamCardShellViewModel?>(null);

        public Task<ZaznamCardDetailViewModel?> BuildRecordCardDetailAsync(int projectId, int recordId, CancellationToken ct = default)
            => Task.FromResult<ZaznamCardDetailViewModel?>(null);

        public Task<ZaznamCommentsPanelViewModel?> BuildRecordCommentsPanelAsync(int projectId, int recordId, CancellationToken ct = default)
            => Task.FromResult<ZaznamCommentsPanelViewModel?>(null);

        public Task<ZaznamCommentsPanelViewModel?> BuildRecordCommentsPanelAsync(int projectId, int recordId, int? limit, bool loadAll, CancellationToken ct = default)
            => Task.FromResult<ZaznamCommentsPanelViewModel?>(null);

        public Task<IReadOnlyList<LookupOptionViewModel>> BuildProjectStatusOptionsAsync(CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.FromResult(ProjectStatusOptions);

        public Task SaveTeamMemberAsync(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveTeamMemberAsync(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> SaveProjectAsync(SaveProjectCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SoftDeleteProjectAsync(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AssignProjectRoleAsync(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeactivateProjectRoleAsync(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AssignProjectSubsystemAsync(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeactivateProjectSubsystemAsync(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AssignProjectSubsystemRoleAsync(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeactivateProjectSubsystemRoleAsync(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeMeetingService : IMeetingService
    {
        public MeetingModalViewModel? NewMeetingModalResult { get; init; }
        public MeetingModalViewModel? EditMeetingModalResult { get; init; }
        public bool? EditableMeetingState { get; init; }

        public Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(IReadOnlyCollection<int>? projectIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JednaniListItemViewModel>> BuildJednaniListAsync(int projektId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MeetingModalViewModel> BuildNewMeetingModalAsync(int projectId, DateTime localNow, CancellationToken ct = default)
            => Task.FromResult(NewMeetingModalResult ?? throw new NotSupportedException());
        public Task<MeetingModalViewModel?> BuildEditMeetingModalAsync(int projectId, int meetingId, CancellationToken ct = default)
            => Task.FromResult(EditMeetingModalResult);
        public Task<bool?> IsMeetingEditableAsync(int projectId, int meetingId, CancellationToken ct = default)
            => Task.FromResult(EditableMeetingState);
        public Task<JednaniDetailViewModel> BuildJednaniDetailAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> GetMeetingProjectIdAsync(int meetingId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<JednaniUkolViewModel?> GetSingleTaskAsync(int meetingId, int zaznamId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MeetingParticipantCandidateViewModel>> BuildMeetingParticipantCandidatesAsync(int projectId, int meetingId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> SaveMeetingAsync(SaveMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteMeetingAsync(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveMeetingStatusAsync(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddMeetingParticipantAsync(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAttendanceBatchAsync(int meetingId, IEnumerable<(int OsobaId, string StavUcasti)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveMeetingNotesBatchAsync(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
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

    private sealed class FakeRecordProposalService : IRecordProposalService
    {
        public Task<bool> CanViewProposalTabAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<ProjektNavrhyTabViewModel> BuildProjectProposalsTabAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.FromResult(new ProjektNavrhyTabViewModel { ProjektId = projectId });

        public Task<ZaznamEditViewModel> BuildCreateRecordProposalEditorAsync(int projectId, CurrentUserContextViewModel currentUser, int? meetingId = null, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ZaznamEditViewModel> BuildScheduleProposalEditorAsync(int projectId, int recordId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ZaznamEditViewModel> BuildProposalDetailAsync(int projectId, int proposalId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ZaznamEditViewModel> BuildEditableRecordEditorFromProposalAsync(int projectId, int proposalId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ZaznamEditViewModel> BuildPrefilledCreateRecordEditorFromProposalAsync(int projectId, int proposalId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task SubmitCreateRecordProposalAsync(SaveRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task SubmitScheduleProposalAsync(SaveRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<int?> ApproveProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task RejectProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task RejectAndTakeOverCreateProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task RejectAndEditProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeProjectDashboardService : IProjectDashboardService
    {
        public Task<ProjectDashboardPageViewModel> BuildDashboardPageAsync(int projectId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ProjectDashboardRecordsPanelViewModel> BuildRecordsPanelAsync(int projectId, DateTime referenceDate, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ProjectDashboardStatisticsPanelViewModel> BuildStatisticsPanelAsync(int projectId, int year, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ProjectDashboardNesPanelViewModel> BuildNesPanelAsync(
            int projektId, DateTime reference, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<ProjectDashboardVyzvyPanelViewModel> BuildVyzvyPanelAsync(
            int projektId, bool muzeEditovat, CancellationToken ct)
            => throw new NotSupportedException();

        // CanAccessDashboardAsync smazáno v redesignu 2026-04-23 — dashboard.view policy na endpointech.
    }
}
