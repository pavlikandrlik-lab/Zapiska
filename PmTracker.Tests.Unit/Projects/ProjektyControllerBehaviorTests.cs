using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Meetings;
using PmTracker.Web.Modules.Projects;

namespace PmTracker.Tests.Unit.Projects;

public sealed class ProjektyControllerBehaviorTests
{
    [Fact]
    public void EditProjectModal_ShouldSelectStatusByLabel_WhenCodeDoesNotExistInOptions()
    {
        var projectId = 42;
        var queries = new FakeProjectsQueries
        {
            ProjektExistsResult = true,
            ProjectStatusOptions =
            [
                new LookupOptionViewModel { Value = "RUN", Label = "Běží" },
                new LookupOptionViewModel { Value = "PLAN", Label = "Plánováno" }
            ],
            ProjektyList =
            [
                new ProjektListItemViewModel
                {
                    Id = projectId,
                    Zkratka = "PRJ",
                    Nazev = "Projekt fallback",
                    StavKod = "LEGACY_STATUS",
                    Stav = "Plánováno",
                    CanEdit = true,
                    CanDelete = true
                }
            ],
            ProjektDetail = CreateEmptyProjektDetail(projectId)
        };
        var controller = CreateController(queries);

        var result = controller.EditProjectModal(projectId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("ProjectModal");
        var model = view.Model.Should().BeOfType<ProjectModalViewModel>().Subject;
        model.Command.Stav.Should().Be("PLAN");
    }

    [Fact]
    public void EditProjectModal_ShouldFallbackToFirstStatus_WhenCodeAndLabelDoNotMatch()
    {
        var projectId = 43;
        var queries = new FakeProjectsQueries
        {
            ProjektExistsResult = true,
            ProjectStatusOptions =
            [
                new LookupOptionViewModel { Value = "RUN", Label = "Běží" },
                new LookupOptionViewModel { Value = "PLAN", Label = "Plánováno" }
            ],
            ProjektyList =
            [
                new ProjektListItemViewModel
                {
                    Id = projectId,
                    Zkratka = "PRJ2",
                    Nazev = "Projekt fallback first",
                    StavKod = "UNKNOWN_CODE",
                    Stav = "Neexistující stav",
                    CanEdit = true,
                    CanDelete = true
                }
            ],
            ProjektDetail = CreateEmptyProjektDetail(projectId)
        };
        var controller = CreateController(queries);

        var result = controller.EditProjectModal(projectId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ProjectModalViewModel>().Subject;
        model.Command.Stav.Should().Be("RUN");
    }

    [Fact]
    public void NewMeetingModal_ShouldUseLocalNowFromTimeProvider_ForDefaultDateAndTime()
    {
        const int projectId = 55;
        var fixedUtcNow = new DateTimeOffset(2026, 7, 9, 14, 45, 0, TimeSpan.Zero);
        var expectedLocalNow = TimeZoneInfo.ConvertTime(fixedUtcNow, TimeZoneInfo.Local).DateTime;
        var timeProvider = new FixedTimeProvider(fixedUtcNow);

        var queries = new FakeProjectsQueries
        {
            ProjektExistsResult = true,
            ProjectStatusOptions = [],
            ProjektyList = [],
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
                        Misto = "Zasedačka",
                        StavKod = "OPEN",
                        Stav = "Otevřeno"
                    }
                ],
                meetingStatuses:
                [
                    new LookupOptionViewModel
                    {
                        Value = "OPEN",
                        Label = "Otevřeno"
                    }
                ])
        };
        var meetingsQueries = new FakeMeetingsQueries
        {
            ProjektExistsResult = true,
            ProjektDetail = queries.ProjektDetail
        };
        var controller = CreateController(queries, timeProvider, meetingsQueries);

        var result = controller.NewMeetingModal(projectId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<MeetingModalViewModel>().Subject;
        model.Command.DatumPlanovane.Should().Be(expectedLocalNow.Date);
        model.Command.CasZacatek.Should().Be(TimeOnly.FromDateTime(expectedLocalNow));
        model.Command.CisloJednani.Should().Be(8);
        model.Command.StavJednani.Should().Be("OPEN");
    }

    private static ProjektyController CreateController(
        FakeProjectsQueries queries,
        TimeProvider? timeProvider = null,
        IMeetingsQueries? meetingsQueries = null)
    {
        var services = new ServiceCollection();
        if (timeProvider is not null)
        {
            services.AddSingleton<TimeProvider>(timeProvider);
        }

        var provider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };

        var resolvedMeetingsQueries = meetingsQueries ?? new FakeMeetingsQueries
        {
            ProjektExistsResult = true,
            ProjektDetail = queries.ProjektDetail
        };

        var controller = new ProjektyController(
            userContextResolver: null!,
            projectsQueries: queries,
            projectsCommands: null!,
            meetingsQueries: resolvedMeetingsQueries,
            meetingsCommands: new FakeMeetingsCommands())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
        controller.TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider());

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
        field.Should().NotBeNull("BaseController musí mít backing field pro CurrentUserContext");
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
                Stav = "Běží"
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

    private sealed class FakeProjectsQueries : IProjectsQueries
    {
        public bool ProjektExistsResult { get; init; }
        public required IReadOnlyList<ProjektListItemViewModel> ProjektyList { get; init; }
        public required IReadOnlyList<LookupOptionViewModel> ProjectStatusOptions { get; init; }
        public required ProjektDetailViewModel ProjektDetail { get; init; }

        public bool ProjektExists(int id) => ProjektExistsResult;

        public IReadOnlyList<ProjektListItemViewModel> BuildProjektyList() => ProjektyList;

        public ProjektDetailViewModel BuildProjektDetail(int id) => ProjektDetail;

        public IReadOnlyList<LookupOptionViewModel> BuildProjectStatusOptions(CurrentUserContextViewModel currentUser)
            => ProjectStatusOptions;
    }

    private sealed class FakeMeetingsQueries : IMeetingsQueries
    {
        public bool ProjektExistsResult { get; init; }
        public required ProjektDetailViewModel ProjektDetail { get; init; }

        public bool ProjektExists(int id) => ProjektExistsResult;

        public ProjektDetailViewModel BuildProjektDetail(int id) => ProjektDetail;

        public IReadOnlyList<JednaniProjektListItemViewModel> BuildJednaniOverview() => [];

        public JednaniDetailViewModel BuildJednaniDetail(int id) => throw new NotSupportedException();
    }

    private sealed class FakeMeetingsCommands : IMeetingsCommands
    {
        public int SaveMeeting(SaveMeetingCommand command, CurrentUserContextViewModel currentUser) => throw new NotSupportedException();

        public void DeleteMeeting(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser) => throw new NotSupportedException();

        public void SaveMeetingStatus(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser) => throw new NotSupportedException();

        public void SaveMeetingNote(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser) => throw new NotSupportedException();

        public void SaveAttendance(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser) => throw new NotSupportedException();

        public void AddMeetingParticipant(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser) => throw new NotSupportedException();
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>(_values, StringComparer.OrdinalIgnoreCase);

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
            => _values = new Dictionary<string, object>(values, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
