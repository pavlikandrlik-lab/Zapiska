using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Meetings;

public sealed class JednaniControllerBehaviorTests
{
    [Fact]
    public async Task TaskItemPartial_ShouldBuildPartialFromSingleTaskAndMeetingListAsync()
    {
        var meetingService = new FakeMeetingService
        {
            MeetingProjectId = 42,
            SingleTask = new JednaniUkolViewModel
            {
                ZaznamId = 15,
                CisloZaznamu = 15,
                CisloViditelne = "15",
                Popis = "Ukol",
                LzeUpravovatVyjadreni = true,
                SubsystemLeadEquivalentOsobaIds = [],
                Vyjadreni = []
            },
            Meetings =
            [
                new JednaniListItemViewModel
                {
                    Id = 7,
                    CisloJednani = 3,
                    Datum = new DateTime(2026, 1, 1),
                    CasZacatek = new TimeOnly(9, 0),
                    Misto = "Zasedacka",
                    Stav = "Otevreno",
                    StavKod = "OPEN"
                }
            ]
        };
        var controller = CreateController(meetingService);

        var result = await controller.TaskItemPartial(7, 15);

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        partial.ViewName.Should().Be("_TaskItemPartial");
        var model = partial.Model.Should().BeOfType<JednaniTaskItemPartialViewModel>().Subject;
        model.ProjektId.Should().Be(42);
        model.JednaniId.Should().Be(7);
        model.JednaniCislo.Should().Be(3);
        model.CanEditRecordNotes.Should().BeTrue();
        model.CanCommentAsSubsystemLeader.Should().BeFalse();
        meetingService.BuildJednaniDetailCalls.Should().Be(0);
        meetingService.GetMeetingProjectIdCalls.Should().Be(1);
        meetingService.GetSingleTaskCalls.Should().Be(1);
        meetingService.BuildJednaniListAsyncCalls.Should().Be(1);
    }

    [Fact]
    public async Task Index_ShouldPassAccessibleProjectFilterToMeetingOverviewAsync()
    {
        var meetingService = new FakeMeetingService
        {
            Overview =
            [
                new JednaniProjektListItemViewModel
                {
                    ProjektId = 42,
                    ProjektNazev = "Projekt 42",
                    Jednani = []
                }
            ]
        };
        var controller = CreateController(meetingService);
        SetCurrentUserContext(controller, new CurrentUserContextViewModel
        {
            OsobaId = 1,
            Jmeno = "Unit",
            Prijmeni = "Tester",
            DisplayName = "Unit Tester",
            Email = "unit@test.local",
            OrganizacniCelek = "Test",
            OrganizacniCelekKod = "TEST",
            IsSuperAdmin = false,
            RoleKody = [],
            VisibleProjectIds = [42],
            DeletedProjectIds = [],
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: false,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        });

        var result = await controller.Index(null);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<JednaniIndexViewModel>().Subject;
        model.Projekty.Should().ContainSingle(x => x.ProjektId == 42);
        meetingService.OverviewProjectIds.Should().Equal(42);
    }

    [Fact]
    public async Task NewMeetingModal_ShouldUseLocalNowFromTimeProvider_ForDefaultDateAndTime()
    {
        const int projectId = 55;
        var fixedUtcNow = new DateTimeOffset(2026, 7, 9, 14, 45, 0, TimeSpan.Zero);
        var expectedLocalNow = TimeZoneInfo.ConvertTime(fixedUtcNow, TimeZoneInfo.Local).DateTime;
        var timeProvider = new FakeTimeProviderLocal(fixedUtcNow);
        var meetingService = new FakeMeetingService
        {
            NewMeetingModalResult = new MeetingModalViewModel
            {
                Title = "Nové jednání",
                Command = new SaveMeetingCommand
                {
                    ProjektId = projectId,
                    CisloJednani = 8,
                    DatumPlanovane = expectedLocalNow.Date,
                    CasZacatek = TimeOnly.FromDateTime(expectedLocalNow),
                    StavJednani = "OPEN"
                },
                ExistingMeetingNumbersCsv = "7",
                StavyJednani =
                [
                    new LookupOptionViewModel
                    {
                        Value = "OPEN",
                        Label = "Otevreno"
                    }
                ]
            }
        };
        var projectService = Moq.Mock.Of<IProjectService>(x =>
            x.ProjektExistsAsync(projectId, It.IsAny<CancellationToken>()) == Task.FromResult(true));
        var controller = CreateController(meetingService, timeProvider, projectService);

        var result = await controller.NewMeetingModal(projectId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<MeetingModalViewModel>().Subject;
        model.Command.DatumPlanovane.Should().Be(expectedLocalNow.Date);
        model.Command.CasZacatek.Should().Be(TimeOnly.FromDateTime(expectedLocalNow));
        model.Command.CisloJednani.Should().Be(8);
        model.Command.StavJednani.Should().Be("OPEN");
        meetingService.BuildNewMeetingModalCalls.Should().Be(1);
    }

    private sealed class FakeTimeProviderLocal : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FakeTimeProviderLocal(DateTimeOffset utcNow) { _utcNow = utcNow; }
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }

    private static JednaniController CreateController(
        FakeMeetingService meetingService,
        TimeProvider? timeProvider = null,
        IProjectService? projectService = null)
    {
        var httpContext = new DefaultHttpContext();
        var controller = new JednaniController(
            userContextResolver: null!,
            timeProvider: timeProvider ?? TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            meetingService,
            projectService ?? Moq.Mock.Of<IProjectService>(x =>
                x.ProjektExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()) == Task.FromResult(true)))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        SetCurrentUserContext(controller, new CurrentUserContextViewModel
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
        });

        return controller;
    }

    private static void SetCurrentUserContext(JednaniController controller, CurrentUserContextViewModel userContext)
    {
        var field = typeof(BaseController).GetField("<CurrentUserContext>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(controller, userContext);
    }

    private sealed class FakeMeetingService : IMeetingService
    {
        public int? MeetingProjectId { get; set; }
        public JednaniUkolViewModel? SingleTask { get; set; }
        public IReadOnlyList<JednaniListItemViewModel> Meetings { get; set; } = [];
        public IReadOnlyList<JednaniProjektListItemViewModel> Overview { get; set; } = [];
        public IReadOnlyList<int>? OverviewProjectIds { get; private set; }
        public IReadOnlyList<MeetingParticipantCandidateViewModel> ParticipantCandidates { get; set; } = [];
        public MeetingModalViewModel? NewMeetingModalResult { get; set; }
        public int BuildJednaniDetailCalls { get; private set; }
        public int GetMeetingProjectIdCalls { get; private set; }
        public int GetSingleTaskCalls { get; private set; }
        public int BuildJednaniListAsyncCalls { get; private set; }
        public int BuildNewMeetingModalCalls { get; private set; }

        public Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(CancellationToken ct = default)
            => Task.FromResult(Overview);

        public Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(IReadOnlyCollection<int>? projectIds, CancellationToken ct = default)
        {
            OverviewProjectIds = projectIds?.ToArray();
            return Task.FromResult(Overview);
        }

        public Task<IReadOnlyList<JednaniListItemViewModel>> BuildJednaniListAsync(int projektId, CancellationToken ct = default)
        {
            BuildJednaniListAsyncCalls += 1;
            return Task.FromResult(Meetings);
        }

        public Task<MeetingModalViewModel> BuildNewMeetingModalAsync(int projectId, DateTime localNow, CancellationToken ct = default)
        {
            BuildNewMeetingModalCalls += 1;
            return Task.FromResult(NewMeetingModalResult ?? throw new NotSupportedException());
        }
        public Task<MeetingModalViewModel?> BuildEditMeetingModalAsync(int projectId, int meetingId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool?> IsMeetingEditableAsync(int projectId, int meetingId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<JednaniDetailViewModel> BuildJednaniDetailAsync(int id, CancellationToken ct = default)
        {
            BuildJednaniDetailCalls += 1;
            throw new InvalidOperationException("Controller nema volat BuildJednaniDetailAsync v tomto flow.");
        }

        public Task<int?> GetMeetingProjectIdAsync(int meetingId, CancellationToken ct = default)
        {
            GetMeetingProjectIdCalls += 1;
            return Task.FromResult(MeetingProjectId);
        }

        public Task<JednaniUkolViewModel?> GetSingleTaskAsync(int meetingId, int zaznamId, CancellationToken ct = default)
        {
            GetSingleTaskCalls += 1;
            return Task.FromResult(SingleTask);
        }

        public Task<IReadOnlyList<MeetingParticipantCandidateViewModel>> BuildMeetingParticipantCandidatesAsync(int projectId, int meetingId, CancellationToken ct = default)
            => Task.FromResult(ParticipantCandidates);

        public Task<int> SaveMeetingAsync(SaveMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteMeetingAsync(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveMeetingStatusAsync(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddMeetingParticipantAsync(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAttendanceBatchAsync(int meetingId, IEnumerable<(int OsobaId, string StavUcasti)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveMeetingNotesBatchAsync(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
    }

}
