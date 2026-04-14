using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Tests.Unit.Meetings;

public sealed class MeetingServiceDelegationTests
{
    [Fact]
    public async Task SaveMeetingNotesBatchAsync_ShouldDelegateToCommentService()
    {
        var commentService = new FakeCommentService();
        var sut = new MeetingService(null!, null!, null!, commentService, new FakeAuditWriteService(), TimeProvider.System);
        var currentUser = BuildCurrentUser();
        var rows = new List<(int ZaznamId, string Text)>
        {
            (12, "Poznamka")
        };

        await sut.SaveMeetingNotesBatchAsync(5, rows, currentUser);

        commentService.LastMeetingId.Should().Be(5);
        commentService.LastRows.Should().BeEquivalentTo(rows);
        commentService.LastUser.Should().BeSameAs(currentUser);
    }

    private static CurrentUserContextViewModel BuildCurrentUser()
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

    private sealed class FakeCommentService : ICommentService
    {
        public int? LastMeetingId { get; private set; }
        public IReadOnlyList<(int ZaznamId, string Text)> LastRows { get; private set; } = [];
        public CurrentUserContextViewModel? LastUser { get; private set; }

        public Task AddCommentAsync(AddCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UpdateCommentAsync(UpdateCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteCommentAsync(DeleteCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SaveMeetingNoteAsync(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SaveMeetingNotesBatchAsync(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        {
            LastMeetingId = meetingId;
            LastRows = rows.ToList();
            LastUser = currentUser;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditWriteService : IAuditWriteService
    {
        public void Add(int? actorOsobaId, AuditWriteEntry entry)
        {
        }

        public Task WriteAsync(int? actorOsobaId, AuditWriteEntry entry, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
