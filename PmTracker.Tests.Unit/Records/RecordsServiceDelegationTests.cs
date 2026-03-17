using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Records.Commands;
using PmTracker.Web.Services.Records.Queries;

namespace PmTracker.Tests.Unit.Records;

public sealed class RecordsServiceDelegationTests
{
    [Fact]
    public void ProjektExists_ShouldDelegateToProjektExistsQueryHandler()
    {
        var projektExistsHandler = new FakeProjektExistsQueryHandler { Result = true };
        var sut = CreateSut(projektExistsHandler: projektExistsHandler);

        var result = sut.ProjektExists(42);

        result.Should().BeTrue();
        projektExistsHandler.LastId.Should().Be(42);
        projektExistsHandler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public void SaveRecord_ShouldDelegateToSaveRecordCommandHandler()
    {
        var saveRecordHandler = new FakeSaveRecordCommandHandler { Result = 77 };
        var sut = CreateSut(saveRecordHandler: saveRecordHandler);
        var command = new SaveRecordCommand
        {
            ProjektId = 8,
            Kategorie = "U",
            Stav = "OPEN",
            Nazev = "Task"
        };
        var currentUser = BuildCurrentUser();

        var result = sut.SaveRecord(command, currentUser);

        result.Should().Be(77);
        saveRecordHandler.LastCommand.Should().BeSameAs(command);
        saveRecordHandler.LastUser.Should().BeSameAs(currentUser);
    }

    private static RecordsService CreateSut(
        FakeProjektExistsQueryHandler? projektExistsHandler = null,
        FakeSaveRecordCommandHandler? saveRecordHandler = null)
    {
        return new RecordsService(
            projektExistsHandler ?? new FakeProjektExistsQueryHandler(),
            new FakeBuildProjektDetailQueryHandler(),
            new FakeBuildZaznamEditQueryHandler(),
            new FakeBuildZaznamCreateQueryHandler(),
            new FakeBuildDeleteRecordModalQueryHandler(),
            saveRecordHandler ?? new FakeSaveRecordCommandHandler(),
            new FakeDeleteRecordCommandHandler(),
            new FakeAssignMeetingIdentifierCommandHandler(),
            new FakeAddCommentCommandHandler(),
            new FakeUpdateCommentCommandHandler(),
            new FakeDeleteCommentCommandHandler());
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

    private sealed class FakeProjektExistsQueryHandler : IProjektExistsQueryHandler
    {
        public bool Result { get; set; }
        public int LastId { get; private set; }
        public int InvocationCount { get; private set; }

        public bool Handle(int id)
        {
            LastId = id;
            InvocationCount += 1;
            return Result;
        }
    }

    private sealed class FakeBuildProjektDetailQueryHandler : IBuildProjektDetailQueryHandler
    {
        public ProjektDetailViewModel Handle(int id)
            => throw new NotSupportedException();
    }

    private sealed class FakeBuildZaznamEditQueryHandler : IBuildZaznamEditQueryHandler
    {
        public ZaznamEditViewModel Handle(int id)
            => throw new NotSupportedException();
    }

    private sealed class FakeBuildZaznamCreateQueryHandler : IBuildZaznamCreateQueryHandler
    {
        public ZaznamEditViewModel Handle(int projektId, int? jednaniId = null)
            => throw new NotSupportedException();
    }

    private sealed class FakeBuildDeleteRecordModalQueryHandler : IBuildDeleteRecordModalQueryHandler
    {
        public DeleteRecordModalViewModel Handle(int projektId, int zaznamId)
            => throw new NotSupportedException();
    }

    private sealed class FakeSaveRecordCommandHandler : ISaveRecordCommandHandler
    {
        public int Result { get; set; }
        public SaveRecordCommand? LastCommand { get; private set; }
        public CurrentUserContextViewModel? LastUser { get; private set; }

        public int Handle(SaveRecordCommand command, CurrentUserContextViewModel currentUser)
        {
            LastCommand = command;
            LastUser = currentUser;
            return Result;
        }
    }

    private sealed class FakeDeleteRecordCommandHandler : IDeleteRecordCommandHandler
    {
        public void Handle(DeleteRecordCommand command, CurrentUserContextViewModel currentUser)
        {
        }
    }

    private sealed class FakeAssignMeetingIdentifierCommandHandler : IAssignMeetingIdentifierCommandHandler
    {
        public void Handle(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser)
        {
        }
    }

    private sealed class FakeAddCommentCommandHandler : IAddCommentCommandHandler
    {
        public void Handle(AddCommentCommand command, CurrentUserContextViewModel currentUser)
        {
        }
    }

    private sealed class FakeUpdateCommentCommandHandler : IUpdateCommentCommandHandler
    {
        public void Handle(UpdateCommentCommand command, CurrentUserContextViewModel currentUser)
        {
        }
    }

    private sealed class FakeDeleteCommentCommandHandler : IDeleteCommentCommandHandler
    {
        public void Handle(DeleteCommentCommand command, CurrentUserContextViewModel currentUser)
        {
        }
    }
}
