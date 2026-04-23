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
    public void NavrhyController_ShouldHavePerActionPolicyForProposalCommands()
    {
        // Per-action redesign 2026-04-23: proposals.* per akce.
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/NavrhyController.cs"));
        code.Should().Contain("[Authorize(Policy = \"permission:proposals.record.create\")]",
            "CreateRecordProposal + SubmitCreateProposal má klíč proposals.record.create");
        code.Should().Contain("[Authorize(Policy = \"permission:proposals.schedule.create\")]",
            "CreateScheduleProposal + SubmitScheduleProposal má klíč proposals.schedule.create");
        code.Should().Contain("[Authorize(Policy = \"permission:proposals.accept\")]",
            "ApproveProposal má klíč proposals.accept");
        code.Should().Contain("[Authorize(Policy = \"permission:proposals.reject\")]",
            "RejectProposal (a RejectAndEditProposal) má klíč proposals.reject");
        code.Should().Contain("[Authorize(Policy = \"permission:proposals.takeover\")]",
            "RejectAndTakeOverCreateProposal má klíč proposals.takeover");
        code.Should().Contain("[Authorize(Policy = \"permission:proposals.edit.own\")]",
            "PrefillCreateProposal má klíč proposals.edit.own");
    }

    [Fact]
    public void NavrhyController_ShouldNotContainEditFromProposal()
    {
        // Per-action redesign 2026-04-23: EditFromProposal byl smazán (bypass workflow).
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/NavrhyController.cs"));
        code.Should().NotContain("public async Task<IActionResult> EditFromProposal",
            "EditFromProposal byl v redesignu smazán — bypass zrušen");
    }

    [Fact]
    public void NavrhyController_ComplexActions_ShouldKeepBodyCheck()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/NavrhyController.cs"));
        // Composite cases (ProposalDetail / PrefillCreateProposal) používají CanViewProposalTabAsync.
        code.Should().Contain("CanViewProposalTabAsync",
            "ProposalDetail/PrefillCreateProposal zachovávají CanViewProposalTabAsync body check");
    }
}
