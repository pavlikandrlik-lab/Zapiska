using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Meetings;
using PmTracker.Web.Modules.Meetings.Commands;

namespace PmTracker.Tests.Unit.Meetings;

public sealed class MeetingsCommandsDelegationTests
{
    [Fact]
    public void SaveMeeting_ShouldDelegateToSaveMeetingCommandHandler()
    {
        var saveMeetingHandler = new FakeSaveMeetingCommandHandler { Result = 321 };
        var sut = CreateSut(saveMeetingHandler: saveMeetingHandler);
        var command = new SaveMeetingCommand
        {
            ProjektId = 10,
            CisloJednani = 1,
            DatumPlanovane = new DateTime(2026, 1, 1),
            CasZacatek = new TimeOnly(8, 30),
            StavJednani = "OPEN"
        };
        var currentUser = BuildCurrentUser();

        var result = sut.SaveMeeting(command, currentUser);

        result.Should().Be(321);
        saveMeetingHandler.LastCommand.Should().BeSameAs(command);
        saveMeetingHandler.LastUser.Should().BeSameAs(currentUser);
    }

    [Fact]
    public void AddMeetingParticipant_ShouldDelegateToAddMeetingParticipantCommandHandler()
    {
        var addParticipantHandler = new FakeAddMeetingParticipantCommandHandler();
        var sut = CreateSut(addMeetingParticipantHandler: addParticipantHandler);
        var command = new AddMeetingParticipantCommand
        {
            ProjektId = 10,
            JednaniId = 12,
            OsobaId = 18
        };
        var currentUser = BuildCurrentUser();

        sut.AddMeetingParticipant(command, currentUser);

        addParticipantHandler.InvocationCount.Should().Be(1);
        addParticipantHandler.LastCommand.Should().BeSameAs(command);
        addParticipantHandler.LastUser.Should().BeSameAs(currentUser);
    }

    [Fact]
    public void SaveMeetingStatus_ShouldDelegateToSaveMeetingStatusCommandHandler()
    {
        var saveStatusHandler = new FakeSaveMeetingStatusCommandHandler();
        var sut = CreateSut(saveMeetingStatusHandler: saveStatusHandler);
        var command = new SaveMeetingStatusCommand
        {
            JednaniId = 44,
            Stav = "OPEN",
            OtevritJednani = true
        };
        var currentUser = BuildCurrentUser();

        sut.SaveMeetingStatus(command, currentUser);

        saveStatusHandler.InvocationCount.Should().Be(1);
        saveStatusHandler.LastCommand.Should().BeSameAs(command);
        saveStatusHandler.LastUser.Should().BeSameAs(currentUser);
    }

    [Fact]
    public void SaveMeetingNote_ShouldDelegateToSaveMeetingNoteCommandHandler()
    {
        var saveNoteHandler = new FakeSaveMeetingNoteCommandHandler();
        var sut = CreateSut(saveMeetingNoteHandler: saveNoteHandler);
        var command = new SaveMeetingNoteCommand
        {
            JednaniId = 7,
            ZaznamId = 11,
            Text = "Poznámka"
        };
        var currentUser = BuildCurrentUser();

        sut.SaveMeetingNote(command, currentUser);

        saveNoteHandler.InvocationCount.Should().Be(1);
        saveNoteHandler.LastCommand.Should().BeSameAs(command);
        saveNoteHandler.LastUser.Should().BeSameAs(currentUser);
    }

    private static MeetingsCommands CreateSut(
        FakeSaveMeetingCommandHandler? saveMeetingHandler = null,
        FakeDeleteMeetingCommandHandler? deleteMeetingHandler = null,
        FakeSaveMeetingStatusCommandHandler? saveMeetingStatusHandler = null,
        FakeSaveMeetingNoteCommandHandler? saveMeetingNoteHandler = null,
        FakeSaveAttendanceCommandHandler? saveAttendanceHandler = null,
        FakeAddMeetingParticipantCommandHandler? addMeetingParticipantHandler = null)
    {
        return new MeetingsCommands(
            saveMeetingHandler ?? new FakeSaveMeetingCommandHandler(),
            deleteMeetingHandler ?? new FakeDeleteMeetingCommandHandler(),
            saveMeetingStatusHandler ?? new FakeSaveMeetingStatusCommandHandler(),
            saveMeetingNoteHandler ?? new FakeSaveMeetingNoteCommandHandler(),
            saveAttendanceHandler ?? new FakeSaveAttendanceCommandHandler(),
            addMeetingParticipantHandler ?? new FakeAddMeetingParticipantCommandHandler());
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

    private abstract class FakeVoidHandlerBase<TCommand>
    {
        public int InvocationCount { get; private set; }
        public TCommand? LastCommand { get; private set; }
        public CurrentUserContextViewModel? LastUser { get; private set; }

        protected void Track(TCommand command, CurrentUserContextViewModel currentUser)
        {
            InvocationCount += 1;
            LastCommand = command;
            LastUser = currentUser;
        }
    }

    private sealed class FakeSaveMeetingCommandHandler : ISaveMeetingCommandHandler
    {
        public int Result { get; set; }
        public SaveMeetingCommand? LastCommand { get; private set; }
        public CurrentUserContextViewModel? LastUser { get; private set; }

        public int Handle(SaveMeetingCommand command, CurrentUserContextViewModel currentUser)
        {
            LastCommand = command;
            LastUser = currentUser;
            return Result;
        }
    }

    private sealed class FakeDeleteMeetingCommandHandler : FakeVoidHandlerBase<DeleteMeetingCommand>, IDeleteMeetingCommandHandler
    {
        public void Handle(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser)
            => Track(command, currentUser);
    }

    private sealed class FakeSaveMeetingStatusCommandHandler : FakeVoidHandlerBase<SaveMeetingStatusCommand>, ISaveMeetingStatusCommandHandler
    {
        public void Handle(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser)
            => Track(command, currentUser);
    }

    private sealed class FakeSaveMeetingNoteCommandHandler : FakeVoidHandlerBase<SaveMeetingNoteCommand>, ISaveMeetingNoteCommandHandler
    {
        public void Handle(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser)
            => Track(command, currentUser);
    }

    private sealed class FakeSaveAttendanceCommandHandler : FakeVoidHandlerBase<SaveAttendanceCommand>, ISaveAttendanceCommandHandler
    {
        public void Handle(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser)
            => Track(command, currentUser);
    }

    private sealed class FakeAddMeetingParticipantCommandHandler : FakeVoidHandlerBase<AddMeetingParticipantCommand>, IAddMeetingParticipantCommandHandler
    {
        public void Handle(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser)
            => Track(command, currentUser);
    }
}
