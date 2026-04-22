using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Dashboard;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Tests.Unit.Records;

public sealed class RecordServiceDelegationTests
{
    [Fact]
    public async Task AddCommentAsync_ShouldDelegateToCommentService()
    {
        var commentService = new FakeCommentService();
        var sut = new RecordService(null!, null!, commentService, null!, null!, null!, new FakePendingScheduleProposalLockEvaluator(), new FakePriorityMatrixRebuildService(), new FakeAuditWriteService(), new FakeHarvestScheduler(), TimeProvider.System);
        var command = new AddCommentCommand
        {
            ZaznamId = 13,
            JednaniId = 4,
            Text = "Komentar"
        };
        var currentUser = BuildCurrentUser();

        await sut.AddCommentAsync(command, currentUser);

        commentService.LastAddCommentCommand.Should().BeSameAs(command);
        commentService.LastAddCommentUser.Should().BeSameAs(currentUser);
    }

    private static CurrentUserContextViewModel BuildCurrentUser()
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 10,
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

    private sealed class FakePendingScheduleProposalLockEvaluator : IPendingScheduleProposalLockEvaluator
    {
        public Task<PendingScheduleProposalLockState> EvaluateAsync(int recordId, CancellationToken ct = default)
            => Task.FromResult(new PendingScheduleProposalLockState(false, null, null, false, false));
    }

    private sealed class FakeCommentService : ICommentService
    {
        public AddCommentCommand? LastAddCommentCommand { get; private set; }
        public CurrentUserContextViewModel? LastAddCommentUser { get; private set; }

        public Task AddCommentAsync(AddCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        {
            LastAddCommentCommand = command;
            LastAddCommentUser = currentUser;
            return Task.CompletedTask;
        }

        public Task UpdateCommentAsync(UpdateCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteCommentAsync(DeleteCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SaveMeetingNoteAsync(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SaveMeetingNotesBatchAsync(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeAuditWriteService : IAuditWriteService
    {
        public void Add(int? actorOsobaId, AuditWriteEntry entry)
        {
        }

        public Task WriteAsync(int? actorOsobaId, AuditWriteEntry entry, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakePriorityMatrixRebuildService : IPriorityMatrixRebuildService
    {
        public Task FullRebuildAsync(CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RebuildForRecordAsync(int recordId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task QueueRebuildForSubsystemAsync(int subsystemId, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeHarvestScheduler : IHarvestScheduler
    {
        public Task ScheduleHarvestAsync(int externiOdkazId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task ScheduleHarvestForRecordAsync(int zaznamId, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
