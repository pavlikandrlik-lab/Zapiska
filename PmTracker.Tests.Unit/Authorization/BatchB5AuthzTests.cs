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
    public void JednaniController_ShouldHaveMeetingsEditPolicy_OnAddParticipantModal()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/JednaniController.cs"));
        code.Should().Contain("[Authorize(Policy = \"permission:meetings.edit\")]",
            "AddMeetingParticipantModal musí mít meetings.edit policy (projektId v route)");
    }

    [Fact]
    public void JednaniController_ShouldRetainBodyChecks_ForFormProjektIdActions()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/JednaniController.cs"));
        // SaveStatus/SaveAttendance/SaveNotes/AddMeetingParticipant nemají projektId v route,
        // takže Policy handler by nemohl extrahovat kontext. Keep body checks.
        code.Should().Contain("CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit",
            "form-level projektId actions zachovávají body check (policy handler neumí číst z form body)");
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
