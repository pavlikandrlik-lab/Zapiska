using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;

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

    [Fact]
    public async Task NewMeetingModal_ShouldUseLocalNowFromTimeProvider_ForDefaultDateAndTime()
    {
        const int projectId = 55;
        var fixedUtcNow = new DateTimeOffset(2026, 7, 9, 14, 45, 0, TimeSpan.Zero);
        var expectedLocalNow = TimeZoneInfo.ConvertTime(fixedUtcNow, TimeZoneInfo.Local).DateTime;
        var timeProvider = new FixedTimeProvider(fixedUtcNow);

        var projectService = new FakeProjectService
        {
            ProjektDetail = CreateEmptyProjektDetail(
                projectId,
                meetings:
                [
                    new JednaniListItemViewModel
                    {
                        Id = 1,
                        CisloJednani = 7,
                        Datum = new DateTime(2026, 1, 1),
                        CasZacatek = new TimeOnly(8, 0),
                        Misto = "Zasedacka",
                        StavKod = "OPEN",
                        Stav = "Otevreno"
                    }
                ],
                meetingStatuses:
                [
                    new LookupOptionViewModel
                    {
                        Value = "OPEN",
                        Label = "Otevreno"
                    }
                ])
        };
        var controller = CreateController(projectService, timeProvider);

        var result = await controller.NewMeetingModal(projectId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<MeetingModalViewModel>().Subject;
        model.Command.DatumPlanovane.Should().Be(expectedLocalNow.Date);
        model.Command.CasZacatek.Should().Be(TimeOnly.FromDateTime(expectedLocalNow));
        model.Command.CisloJednani.Should().Be(8);
        model.Command.StavJednani.Should().Be("OPEN");
    }

    private static ProjektyController CreateController(
        FakeProjectService projectService,
        TimeProvider? timeProvider = null)
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
            meetingService: new FakeMeetingService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

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
            PermissionGrants = []
        };
    }

    private static void SetCurrentUserContext(ProjektyController controller, CurrentUserContextViewModel userContext)
    {
        var field = typeof(BaseController).GetField("<CurrentUserContext>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull("BaseController musi mit backing field pro CurrentUserContext");
        field!.SetValue(controller, userContext);
    }

    private static ProjektDetailViewModel CreateEmptyProjektDetail(
        int projectId,
        IReadOnlyList<JednaniListItemViewModel>? meetings = null,
        IReadOnlyList<LookupOptionViewModel>? meetingStatuses = null)
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
            DleSubsystemu = true,
            SkupinySubsystemu = [],
            Zaznamy = [],
            Jednani = meetings ?? [],
            AktivniRole = [],
            HistorieRoli = [],
            AktivniSubsystemyProjektu = [],
            DostupneOsobyProRole = [],
            DostupneProjektoveSubsystemy = [],
            HarmonogramUkoly = [],
            RoleProjektu = [],
            RoleSubsystemu = [],
            DostupneSubsystemy = [],
            OtevrenaJednani = [],
            StavyJednani = meetingStatuses ?? [],
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
        };
    }

    private sealed class FakeProjectService : IProjectService
    {
        public IReadOnlyList<ProjektListItemViewModel> ProjektyList { get; init; } = [];
        public IReadOnlyList<LookupOptionViewModel> ProjectStatusOptions { get; init; } = [];
        public ProjektDetailViewModel ProjektDetail { get; init; } = CreateEmptyProjektDetail(0);

        public Task<bool> ProjektExistsAsync(int id, CancellationToken ct = default)
            => Task.FromResult(ProjektyList.Any(item => item.Id == id) || ProjektDetail.Projekt.Id == id);

        public Task<IReadOnlyList<ProjektListItemViewModel>> BuildProjektyListAsync(CancellationToken ct = default)
            => Task.FromResult(ProjektyList);

        public Task<ProjektDetailViewModel> BuildProjektDetailAsync(int id, CancellationToken ct = default)
            => Task.FromResult(ProjektDetail);

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
        public Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JednaniListItemViewModel>> BuildJednaniListAsync(int projektId, CancellationToken ct = default) => throw new NotSupportedException();
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
}
