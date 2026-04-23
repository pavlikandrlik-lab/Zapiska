using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class BatchB5AuthzTests
{
    [Fact]
    public void CiselnikyController_ShouldHaveCiselnikyEditPolicy_OnRowEditActions()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/CiselnikyController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:ciselniky.edit\")]",
            "CiselnikyEdit body gate musí být nahrazeno Policy atributem");
        code.Should().NotContain("CurrentUserContext.HasPermission(PermissionKeys.CiselnikyEdit",
            "body check CiselnikyEdit musí být nahrazen Policy atributem (DictionarySecurityPolicy sub-checks mohou zůstat)");
    }

    [Fact]
    public void CiselnikyController_ShouldRetain_DictionarySecurityPolicyChecks()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/CiselnikyController.cs"));
        code.Should().Contain("DictionarySecurityPolicy.CanAccessDictionary",
            "row-level DictionarySecurityPolicy sub-checks musí zůstat (sémantika locked-row atd.)");
    }

    [Fact]
    public void JednaniController_ShouldHavePerActionPolicy_OnAddParticipantModal()
    {
        // Per-action redesign 2026-04-23: AddMeetingParticipantModal má klíč
        // meetings.participant.add (dříve meetings.edit).
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/JednaniController.cs"));
        code.Should().Contain("[Authorize(Policy = \"permission:meetings.participant.add\")]",
            "AddMeetingParticipantModal má per-action klíč meetings.participant.add");
    }

    [Fact]
    public void JednaniController_ShouldRetainBodyChecks_ForFormProjektIdActions()
    {
        // Per-action redesign 2026-04-23: SaveStatus/SaveAttendance/SaveNotes/AddMeetingParticipant
        // mají per-action klíče v body check (projektId ve form body, ne v route).
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/JednaniController.cs"));
        code.Should().Contain("PermissionKeys.MeetingsStatusChange",
            "SaveStatus má per-action klíč meetings.status.change");
        code.Should().Contain("PermissionKeys.MeetingsAttendanceEdit",
            "SaveAttendance má per-action klíč meetings.attendance.edit");
        code.Should().Contain("PermissionKeys.MeetingsParticipantAdd",
            "AddMeetingParticipant má per-action klíč meetings.participant.add");
        code.Should().Contain("PermissionKeys.MeetingsNotesEdit",
            "SaveNotes má per-action klíč meetings.notes.edit");
    }

    [Fact]
    public void VyzvyController_ShouldHaveAuthorizeAtClassLevel()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/VyzvyController.cs"));
        code.Should().MatchRegex(
            @"\[Authorize\](\s|\r|\n)+(?:\[[^\]]+\](\s|\r|\n)+)*public\s+(sealed\s+)?class\s+VyzvyController",
            "VyzvyController musí mít class-level [Authorize] (zajišťuje autentikaci pro SetZaradid/Prerdit)");
    }
}
