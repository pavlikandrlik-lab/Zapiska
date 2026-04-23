using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class RecordsProposalsAuthzTests
{
    [Fact]
    public void ZaznamyModals_ShouldHavePerActionPolicy()
    {
        // Per-action redesign 2026-04-23: DeleteRecordModal → records.delete,
        // AssignMeetingIdentifierModal → records.assign.meeting (dříve oba records.edit).
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Modals.cs"));
        code.Should().Contain("[Authorize(Policy = \"permission:records.delete\")]",
            "DeleteRecordModal má specifický klíč records.delete");
        code.Should().Contain("[Authorize(Policy = \"permission:records.assign.meeting\")]",
            "AssignMeetingIdentifierModal má specifický klíč records.assign.meeting");
    }

    [Fact]
    public void ZaznamyController_Create_ShouldHaveRecordsEditPolicy()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.cs"));
        // Either class-level [Authorize(Policy="permission:records.edit")] OR on Create method specifically.
        code.Should().Contain("\"permission:records.edit\"",
            "ZaznamyController.Create musí mít permission:records.edit policy");
    }

    [Fact]
    public void NavrhyController_ShouldHaveSubsystemLeadPolicyForProposalCommands()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/NavrhyController.cs"));
        code.Should().Contain("[Authorize(Policy = \"permission:records.comment.subsystemlead\")]");
        code.Should().NotContain("CurrentUserContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead",
            "body check RecordsCommentSubsystemLead musí být nahrazen Policy atributem na čistých actions");
    }

    [Fact]
    public void NavrhyController_ComplexActions_ShouldKeepBodyCheck()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/NavrhyController.cs"));
        // Composite OR checks stay in body — look for CanViewProposalTabAsync call
        code.Should().Contain("CanViewProposalTabAsync",
            "Complex OR cases (ProposalDetail/EditFromProposal/PrefillCreateProposal) stále používají CanViewProposalTabAsync body check");
    }
}
