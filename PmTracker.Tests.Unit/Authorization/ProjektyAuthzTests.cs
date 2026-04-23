using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ProjektyAuthzTests
{
    [Fact]
    public void ProjektyCommands_ShouldUsePolicyForDeleteActions()
    {
        // Per-action redesign 2026-04-23: team.* akce mají per-action klíče
        // (team.member.add / .role.assign / .subsystem.create / ...). Policy atribut
        // nejde použít — projektId je ve form body, ne v route — takže se každý klíč
        // kontroluje imperativně v body přes ExecuteTeamValidatedActionAsync.
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.Commands.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:projects.delete\")]",
            "DeleteProject musí mít projects.delete");
        code.Should().Contain("CurrentUserContext.HasPermission(permissionKey, projektId)",
            "team.* akce provádějí per-project check v body (helper ExecuteTeamValidatedActionAsync / ExecuteTeamActionAsync)");
        code.Should().Contain("PermissionKeys.TeamMemberAdd",
            "SaveTeamMember musí delegovat na team.member.add klíč");
        code.Should().Contain("PermissionKeys.TeamSubsystemReorder",
            "ReorderProjectSubsystem musí delegovat na team.subsystem.reorder klíč");

        // Per-action redesign 2026-04-23: JednaniController.Delete má meetings.delete
        // (specifický klíč pro delete, nikoli sdílený meetings.edit).
        var jednaniCommands = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/JednaniController.Commands.cs"));
        jednaniCommands.Should().Contain("[Authorize(Policy = \"permission:meetings.delete\")]",
            "JednaniController.Delete má per-action klíč meetings.delete");
    }

    [Fact]
    public void ProjektyProjectModals_ShouldUseCorrectPolicies()
    {
        // Per-action redesign 2026-04-23: team.manage rozděleno na per-action klíče.
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.ProjectModals.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:projects.create\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:projects.edit\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:projects.delete\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:team.member.add\")]",
            "AddTeamMemberModal má per-action klíč team.member.add");
        code.Should().Contain("[Authorize(Policy = \"permission:team.role.assign\")]",
            "AssignProjectRoleModal má per-action klíč team.role.assign");
        code.Should().Contain("[Authorize(Policy = \"permission:team.subsystem.create\")]",
            "AssignProjectSubsystemModal má per-action klíč team.subsystem.create");
        code.Should().Contain("[Authorize(Policy = \"permission:team.subsystem.role.assign\")]",
            "AssignProjectSubsystemRoleModal má per-action klíč team.subsystem.role.assign");
    }

    [Fact]
    public void JednaniMeetingModals_ShouldUseCorrectPolicies()
    {
        // Meeting modaly přesunuty z ProjektyController.MeetingModals.cs do
        // JednaniController.Modals.cs 2026-04-23.
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/JednaniController.Modals.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:meetings.create\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:meetings.edit\")]");
    }

    [Fact]
    public void ProjektyCommands_ShouldNotHaveBody_ProjectsDeleteCheck()
    {
        // projects.delete má klasický [Authorize(Policy)] a projektId je v DeleteProject
        // command (form body) — ale projects.delete policy je použitelná globálně (nebo bez
        // scoped kontroly, super-admin only v praxi) → policy atribut zůstává.
        // team.manage check NAOPAK patří do body (viz ProjektyCommands_ShouldUsePolicyForDeleteActions).
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.Commands.cs"));

        code.Should().NotContain("CurrentUserContext.HasPermission(PermissionKeys.ProjectsDelete",
            "projects.delete body check musí být nahrazen Policy atributem");
    }

    [Fact]
    public void ProjektyController_Main_ShouldHaveClassLevelAuthorize()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.cs"));

        code.Should().MatchRegex(
            @"\[Authorize[^\]]*\](\s|\r|\n)+(?:\[[^\]]+\](\s|\r|\n)+)*public\s+(sealed\s+)?partial\s+class\s+ProjektyController",
            "ProjektyController (main) musí mít class-level [Authorize]");
    }
}
