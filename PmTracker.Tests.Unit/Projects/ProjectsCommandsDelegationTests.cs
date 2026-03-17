using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Projects;
using PmTracker.Web.Modules.Projects.Commands;

namespace PmTracker.Tests.Unit.Projects;

public sealed class ProjectsCommandsDelegationTests
{
    [Fact]
    public void SaveProject_ShouldDelegateToSaveProjectCommandHandler()
    {
        var saveHandler = new FakeSaveProjectCommandHandler
        {
            Result = 123
        };
        var softDeleteHandler = new FakeSoftDeleteProjectCommandHandler();
        var command = new SaveProjectCommand
        {
            Nazev = "Projekt A",
            Zkratka = "PA",
            Stav = "ACTIVE"
        };
        var currentUser = BuildCurrentUser();
        var sut = CreateSut(saveHandler: saveHandler, softDeleteHandler: softDeleteHandler);

        var result = sut.SaveProject(command, currentUser);

        result.Should().Be(123);
        saveHandler.LastCommand.Should().BeSameAs(command);
        saveHandler.LastUser.Should().BeSameAs(currentUser);
        softDeleteHandler.InvocationCount.Should().Be(0);
    }

    [Fact]
    public void SoftDeleteProject_ShouldDelegateToSoftDeleteProjectCommandHandler()
    {
        var saveHandler = new FakeSaveProjectCommandHandler();
        var softDeleteHandler = new FakeSoftDeleteProjectCommandHandler();
        var command = new SoftDeleteProjectCommand
        {
            ProjektId = 77,
            PotvrditSmazani = true
        };
        var currentUser = BuildCurrentUser();
        var sut = CreateSut(saveHandler: saveHandler, softDeleteHandler: softDeleteHandler);

        sut.SoftDeleteProject(command, currentUser);

        softDeleteHandler.InvocationCount.Should().Be(1);
        softDeleteHandler.LastCommand.Should().BeSameAs(command);
        softDeleteHandler.LastUser.Should().BeSameAs(currentUser);
    }

    [Fact]
    public void SaveTeamMember_ShouldDelegateToSaveTeamMemberCommandHandler()
    {
        var saveTeamMemberHandler = new FakeSaveTeamMemberCommandHandler();
        var command = new SaveTeamMemberCommand
        {
            ProjektId = 42,
            OsobaId = 12,
            Role = "OWNER"
        };
        var currentUser = BuildCurrentUser();
        var sut = CreateSut(saveTeamMemberHandler: saveTeamMemberHandler);

        sut.SaveTeamMember(command, currentUser);

        saveTeamMemberHandler.InvocationCount.Should().Be(1);
        saveTeamMemberHandler.LastCommand.Should().BeSameAs(command);
        saveTeamMemberHandler.LastUser.Should().BeSameAs(currentUser);
    }

    [Fact]
    public void AssignProjectSubsystemRole_ShouldDelegateToAssignProjectSubsystemRoleCommandHandler()
    {
        var assignSubsystemRoleHandler = new FakeAssignProjectSubsystemRoleCommandHandler();
        var command = new AssignProjectSubsystemRoleCommand
        {
            ProjektId = 50,
            ProjektSubsystemId = 5,
            OsobaId = 7,
            RoleKod = "LEAD"
        };
        var currentUser = BuildCurrentUser();
        var sut = CreateSut(assignProjectSubsystemRoleHandler: assignSubsystemRoleHandler);

        sut.AssignProjectSubsystemRole(command, currentUser);

        assignSubsystemRoleHandler.InvocationCount.Should().Be(1);
        assignSubsystemRoleHandler.LastCommand.Should().BeSameAs(command);
        assignSubsystemRoleHandler.LastUser.Should().BeSameAs(currentUser);
    }

    private static ProjectsCommands CreateSut(
        FakeSaveProjectCommandHandler? saveHandler = null,
        FakeSoftDeleteProjectCommandHandler? softDeleteHandler = null,
        FakeSaveTeamMemberCommandHandler? saveTeamMemberHandler = null,
        FakeRemoveTeamMemberCommandHandler? removeTeamMemberHandler = null,
        FakeAssignProjectRoleCommandHandler? assignProjectRoleHandler = null,
        FakeDeactivateProjectRoleCommandHandler? deactivateProjectRoleHandler = null,
        FakeAssignProjectSubsystemCommandHandler? assignProjectSubsystemHandler = null,
        FakeDeactivateProjectSubsystemCommandHandler? deactivateProjectSubsystemHandler = null,
        FakeAssignProjectSubsystemRoleCommandHandler? assignProjectSubsystemRoleHandler = null,
        FakeDeactivateProjectSubsystemRoleCommandHandler? deactivateProjectSubsystemRoleHandler = null)
    {
        return new ProjectsCommands(
            saveHandler ?? new FakeSaveProjectCommandHandler(),
            softDeleteHandler ?? new FakeSoftDeleteProjectCommandHandler(),
            saveTeamMemberHandler ?? new FakeSaveTeamMemberCommandHandler(),
            removeTeamMemberHandler ?? new FakeRemoveTeamMemberCommandHandler(),
            assignProjectRoleHandler ?? new FakeAssignProjectRoleCommandHandler(),
            deactivateProjectRoleHandler ?? new FakeDeactivateProjectRoleCommandHandler(),
            assignProjectSubsystemHandler ?? new FakeAssignProjectSubsystemCommandHandler(),
            deactivateProjectSubsystemHandler ?? new FakeDeactivateProjectSubsystemCommandHandler(),
            assignProjectSubsystemRoleHandler ?? new FakeAssignProjectSubsystemRoleCommandHandler(),
            deactivateProjectSubsystemRoleHandler ?? new FakeDeactivateProjectSubsystemRoleCommandHandler());
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

        protected void TrackCall(TCommand command, CurrentUserContextViewModel currentUser)
        {
            InvocationCount += 1;
            LastCommand = command;
            LastUser = currentUser;
        }
    }

    private sealed class FakeSaveProjectCommandHandler : ISaveProjectCommandHandler
    {
        public int Result { get; set; }
        public SaveProjectCommand? LastCommand { get; private set; }
        public CurrentUserContextViewModel? LastUser { get; private set; }

        public int Handle(SaveProjectCommand command, CurrentUserContextViewModel currentUser)
        {
            LastCommand = command;
            LastUser = currentUser;
            return Result;
        }
    }

    private sealed class FakeSoftDeleteProjectCommandHandler : FakeVoidHandlerBase<SoftDeleteProjectCommand>, ISoftDeleteProjectCommandHandler
    {
        public void Handle(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser)
            => TrackCall(command, currentUser);
    }

    private sealed class FakeSaveTeamMemberCommandHandler : FakeVoidHandlerBase<SaveTeamMemberCommand>, ISaveTeamMemberCommandHandler
    {
        public void Handle(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser)
            => TrackCall(command, currentUser);
    }

    private sealed class FakeRemoveTeamMemberCommandHandler : FakeVoidHandlerBase<RemoveTeamMemberCommand>, IRemoveTeamMemberCommandHandler
    {
        public void Handle(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser)
            => TrackCall(command, currentUser);
    }

    private sealed class FakeAssignProjectRoleCommandHandler : FakeVoidHandlerBase<AssignProjectRoleCommand>, IAssignProjectRoleCommandHandler
    {
        public void Handle(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser)
            => TrackCall(command, currentUser);
    }

    private sealed class FakeDeactivateProjectRoleCommandHandler : FakeVoidHandlerBase<DeactivateProjectRoleCommand>, IDeactivateProjectRoleCommandHandler
    {
        public void Handle(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser)
            => TrackCall(command, currentUser);
    }

    private sealed class FakeAssignProjectSubsystemCommandHandler : FakeVoidHandlerBase<AssignProjectSubsystemCommand>, IAssignProjectSubsystemCommandHandler
    {
        public void Handle(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser)
            => TrackCall(command, currentUser);
    }

    private sealed class FakeDeactivateProjectSubsystemCommandHandler : FakeVoidHandlerBase<DeactivateProjectSubsystemCommand>, IDeactivateProjectSubsystemCommandHandler
    {
        public void Handle(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser)
            => TrackCall(command, currentUser);
    }

    private sealed class FakeAssignProjectSubsystemRoleCommandHandler : FakeVoidHandlerBase<AssignProjectSubsystemRoleCommand>, IAssignProjectSubsystemRoleCommandHandler
    {
        public void Handle(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser)
            => TrackCall(command, currentUser);
    }

    private sealed class FakeDeactivateProjectSubsystemRoleCommandHandler : FakeVoidHandlerBase<DeactivateProjectSubsystemRoleCommand>, IDeactivateProjectSubsystemRoleCommandHandler
    {
        public void Handle(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser)
            => TrackCall(command, currentUser);
    }
}
