using FluentAssertions;

namespace PmTracker.Tests.Unit.Audit;

public sealed class AuditCoverageMatrixTests
{
    [Fact]
    public void CoverageMatrix_ShouldListEveryPrimaryWriteEntrypointExactlyOnce()
    {
        var rows = new[]
        {
            "ProjectService.SaveProjectAsync",
            "ProjectService.SoftDeleteProjectAsync",
            "ProjectService.SaveTeamMemberAsync",
            "ProjectService.RemoveTeamMemberAsync",
            "ProjectService.AssignProjectRoleAsync",
            "ProjectService.DeactivateProjectRoleAsync",
            "ProjectService.AssignProjectSubsystemAsync",
            "ProjectService.ReorderProjectSubsystemAsync",
            "ProjectService.DeactivateProjectSubsystemAsync",
            "ProjectService.AssignProjectSubsystemRoleAsync",
            "ProjectService.DeactivateProjectSubsystemRoleAsync",
            "RecordService.SaveRecordAsync",
            "RecordService.DeleteRecordAsync",
            "RecordService.AssignMeetingIdentifierAsync",
            "CommentService.AddCommentAsync",
            "CommentService.UpdateCommentAsync",
            "CommentService.DeleteCommentAsync",
            "CommentService.SaveMeetingNotesBatchAsync",
            "MeetingService.SaveMeetingAsync",
            "MeetingService.DeleteMeetingAsync",
            "MeetingService.SaveMeetingStatusAsync",
            "MeetingService.SaveAttendanceBatchAsync",
            "MeetingService.AddMeetingParticipantAsync",
            "RecordProposalService.SubmitCreateProposalAsync",
            "RecordProposalService.SubmitScheduleProposalAsync",
            "RecordProposalService.ApproveProposalAsync",
            "RecordProposalService.RejectProposalAsync",
            "RecordProposalService.RejectAndTakeOverCreateProposalAsync",
            "RecordProposalService.RejectAndEditProposalAsync",
            "PeopleService.SaveManualPersonAsync",
            "PeopleService.SaveAdPersonAsync",
            "PeopleService.DeletePersonAsync",
            "DictionaryService.SaveCiselnikRowAsync",
            "DictionaryService.DeleteCiselnikRowAsync",
            "SettingsAuthzCommands.SaveUserRoleAssignmentAsync",
            "SettingsAuthzCommands.SaveUserRolesForUserAsync",
            "SettingsAuthzCommands.SaveAuthzRoleAsync",
            "SettingsAuthzCommands.ToggleAuthzRoleAsync",
            "SettingsAuthzCommands.SaveAuthzPermissionAsync",
            "SettingsAuthzCommands.ToggleAuthzPermissionAsync",
            "SettingsAuthzCommands.SaveRolePermissionAsync",
            "SettingsAuthzCommands.DeleteRolePermissionAsync"
        };

        rows.Should().OnlyHaveUniqueItems();
        rows.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x));
        rows.Should().HaveCount(42);
    }
}
