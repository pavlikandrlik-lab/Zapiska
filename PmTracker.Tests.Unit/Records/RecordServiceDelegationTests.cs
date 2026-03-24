using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;

namespace PmTracker.Tests.Unit.Records;

public sealed class RecordServiceDelegationTests
{
    [Fact]
    public async Task AddCommentAsync_ShouldDelegateToCommentService()
    {
        var commentService = new FakeCommentService();
        var sut = new RecordService(null!, null!, commentService, null!, null!, null!, TimeProvider.System);
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
            PermissionGrants = []
        };
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
}
