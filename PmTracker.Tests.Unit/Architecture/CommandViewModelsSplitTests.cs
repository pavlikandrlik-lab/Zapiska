using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class CommandViewModelsSplitTests
{
    [Fact]
    public void OriginalMonolithFile_ShouldBeDeleted()
    {
        File.Exists(ResolvePath("PmTracker.Web/Models/ViewModels/CommandViewModels.cs"))
            .Should().BeFalse("CommandViewModels.cs monolith musí být smazán po splitu — Fáze 3D Task 4");
    }

    [Theory]
    [InlineData("PmTracker.Web/Models/ViewModels/Commands/ProjectCommands.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Commands/MeetingCommands.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Commands/ProposalCommands.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Commands/DictionaryCommands.cs")]
    public void NewSplitFile_ShouldExistWithCorrectNamespace(string relativePath)
    {
        File.Exists(ResolvePath(relativePath))
            .Should().BeTrue($"{relativePath} musí existovat — Fáze 3D Task 4");

        var content = File.ReadAllText(ResolvePath(relativePath));
        content.Should().Contain("namespace PmTracker.Web.Models.ViewModels;",
            $"{relativePath} musí používat namespace PmTracker.Web.Models.ViewModels (file-scoped) — zero consumer breakage");
    }

    [Fact]
    public void ProjectCommands_ShouldContainProjectAndTeamTypes()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Commands/ProjectCommands.cs"));
        content.Should().Contain("SaveProjectCommand", "ProjectCommands musí obsahovat SaveProjectCommand");
        content.Should().Contain("SoftDeleteProjectCommand", "ProjectCommands musí obsahovat SoftDeleteProjectCommand");
        content.Should().Contain("SaveTeamMemberCommand", "ProjectCommands musí obsahovat SaveTeamMemberCommand");
        content.Should().Contain("RemoveTeamMemberCommand", "ProjectCommands musí obsahovat RemoveTeamMemberCommand");
        content.Should().Contain("AssignProjectRoleCommand", "ProjectCommands musí obsahovat AssignProjectRoleCommand");
        content.Should().Contain("DeactivateProjectRoleCommand", "ProjectCommands musí obsahovat DeactivateProjectRoleCommand");
        content.Should().Contain("AssignProjectSubsystemCommand", "ProjectCommands musí obsahovat AssignProjectSubsystemCommand");
        content.Should().Contain("ReorderProjectSubsystemCommand", "ProjectCommands musí obsahovat ReorderProjectSubsystemCommand");
        content.Should().Contain("ProjectSubsystemReorderDirections", "ProjectCommands musí obsahovat ProjectSubsystemReorderDirections");
    }

    [Fact]
    public void ProjectCommands_ShouldNotContainOtherDomainTypes()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Commands/ProjectCommands.cs"));
        content.Should().NotContain("SaveMeetingCommand", "ProjectCommands nesmí obsahovat MeetingCommand typy");
        content.Should().NotContain("SaveRecordCommand", "ProjectCommands nesmí obsahovat RecordCommand typy");
        content.Should().NotContain("ProposalDecisionCommand", "ProjectCommands nesmí obsahovat ProposalCommand typy");
        content.Should().NotContain("SaveCiselnikRowCommand", "ProjectCommands nesmí obsahovat DictionaryCommand typy");
    }

    [Fact]
    public void MeetingCommands_ShouldContainMeetingTypes()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Commands/MeetingCommands.cs"));
        content.Should().Contain("SaveMeetingCommand", "MeetingCommands musí obsahovat SaveMeetingCommand");
        content.Should().Contain("DeleteMeetingCommand", "MeetingCommands musí obsahovat DeleteMeetingCommand");
        content.Should().Contain("SaveMeetingStatusCommand", "MeetingCommands musí obsahovat SaveMeetingStatusCommand");
        content.Should().Contain("SaveMeetingNoteCommand", "MeetingCommands musí obsahovat SaveMeetingNoteCommand");
        content.Should().Contain("SaveAttendanceCommand", "MeetingCommands musí obsahovat SaveAttendanceCommand");
        content.Should().Contain("AddMeetingParticipantCommand", "MeetingCommands musí obsahovat AddMeetingParticipantCommand");
        content.Should().Contain("AssignMeetingIdentifierCommand", "MeetingCommands musí obsahovat AssignMeetingIdentifierCommand");
    }

    [Fact]
    public void MeetingCommands_ShouldNotContainOtherDomainTypes()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Commands/MeetingCommands.cs"));
        content.Should().NotContain("SaveProjectCommand", "MeetingCommands nesmí obsahovat ProjectCommand typy");
        content.Should().NotContain("SaveRecordCommand", "MeetingCommands nesmí obsahovat RecordCommand typy");
        content.Should().NotContain("ProposalDecisionCommand", "MeetingCommands nesmí obsahovat ProposalCommand typy");
    }

    [Fact]
    public void RecordCommands_ShouldContainRecordTypes()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs"));
        content.Should().Contain("SaveRecordCommand", "RecordCommands musí obsahovat SaveRecordCommand");
        content.Should().Contain("DeleteRecordCommand", "RecordCommands musí obsahovat DeleteRecordCommand");
        content.Should().Contain("SaveRecordExterniVazbaCommand", "RecordCommands musí obsahovat SaveRecordExterniVazbaCommand");
        content.Should().Contain("SaveRecordHarmonogramValueCommand", "RecordCommands musí obsahovat SaveRecordHarmonogramValueCommand");
        content.Should().Contain("AddCommentCommand", "RecordCommands musí obsahovat AddCommentCommand");
        content.Should().Contain("UpdateCommentCommand", "RecordCommands musí obsahovat UpdateCommentCommand");
        content.Should().Contain("DeleteCommentCommand", "RecordCommands musí obsahovat DeleteCommentCommand");
    }

    [Fact]
    public void RecordCommands_ShouldNotContainOtherDomainTypes()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs"));
        content.Should().NotContain("SaveProjectCommand", "RecordCommands nesmí obsahovat ProjectCommand typy");
        content.Should().NotContain("SaveMeetingCommand", "RecordCommands nesmí obsahovat MeetingCommand typy");
        content.Should().NotContain("ProposalDecisionCommand", "RecordCommands nesmí obsahovat ProposalCommand typy");
        content.Should().NotContain("SaveCiselnikRowCommand", "RecordCommands nesmí obsahovat DictionaryCommand typy");
    }

    [Fact]
    public void ProposalCommands_ShouldContainProposalTypes()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Commands/ProposalCommands.cs"));
        content.Should().Contain("ProposalDecisionCommand", "ProposalCommands musí obsahovat ProposalDecisionCommand");
        content.Should().Contain("PrefillCreateProposalCommand", "ProposalCommands musí obsahovat PrefillCreateProposalCommand");
    }

    [Fact]
    public void ProposalCommands_ShouldNotContainOtherDomainTypes()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Commands/ProposalCommands.cs"));
        content.Should().NotContain("SaveProjectCommand", "ProposalCommands nesmí obsahovat ProjectCommand typy");
        content.Should().NotContain("SaveMeetingCommand", "ProposalCommands nesmí obsahovat MeetingCommand typy");
        content.Should().NotContain("SaveRecordCommand", "ProposalCommands nesmí obsahovat RecordCommand typy");
        content.Should().NotContain("SaveCiselnikRowCommand", "ProposalCommands nesmí obsahovat DictionaryCommand typy");
    }

    [Fact]
    public void DictionaryCommands_ShouldContainDictionaryAndAuthzTypes()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Commands/DictionaryCommands.cs"));
        content.Should().Contain("SaveCiselnikRowCommand", "DictionaryCommands musí obsahovat SaveCiselnikRowCommand");
        content.Should().Contain("DeleteCiselnikRowCommand", "DictionaryCommands musí obsahovat DeleteCiselnikRowCommand");
        content.Should().Contain("SaveAuthzRoleCommand", "DictionaryCommands musí obsahovat SaveAuthzRoleCommand");
        content.Should().Contain("SaveAuthzPermissionCommand", "DictionaryCommands musí obsahovat SaveAuthzPermissionCommand");
        content.Should().Contain("SaveUserRoleAssignmentCommand", "DictionaryCommands musí obsahovat SaveUserRoleAssignmentCommand");
        content.Should().Contain("SaveManualPersonCommand", "DictionaryCommands musí obsahovat SaveManualPersonCommand");
        content.Should().Contain("SaveAdPersonCommand", "DictionaryCommands musí obsahovat SaveAdPersonCommand");
        content.Should().Contain("DeletePersonCommand", "DictionaryCommands musí obsahovat DeletePersonCommand");
    }

    [Fact]
    public void DictionaryCommands_ShouldNotContainOtherDomainTypes()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Commands/DictionaryCommands.cs"));
        content.Should().NotContain("SaveProjectCommand", "DictionaryCommands nesmí obsahovat ProjectCommand typy");
        content.Should().NotContain("SaveMeetingCommand", "DictionaryCommands nesmí obsahovat MeetingCommand typy");
        content.Should().NotContain("SaveRecordCommand", "DictionaryCommands nesmí obsahovat RecordCommand typy");
        content.Should().NotContain("ProposalDecisionCommand", "DictionaryCommands nesmí obsahovat ProposalCommand typy");
    }
}
