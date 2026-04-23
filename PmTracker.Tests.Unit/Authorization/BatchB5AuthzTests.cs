using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class BatchB5AuthzTests
{
    [Fact]
    public void CiselnikyController_ShouldHavePerActionPolicy_OnRowActions()
    {
        // Per-action redesign 2026-04-23: ciselniky.edit → row.edit / row.delete.
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/CiselnikyController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:ciselniky.row.edit\")]",
            "EditRow + SaveRow mají per-action klíč ciselniky.row.edit");
        code.Should().Contain("[Authorize(Policy = \"permission:ciselniky.row.delete\")]",
            "DeleteRow má per-action klíč ciselniky.row.delete");
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
