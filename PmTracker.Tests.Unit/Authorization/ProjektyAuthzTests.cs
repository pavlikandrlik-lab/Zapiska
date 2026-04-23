using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ProjektyAuthzTests
{
    [Fact]
    public void ProjektyCommands_ShouldUsePolicyForDeleteActions()
    {
        // H-1 IDOR fix: team-management akce v ProjektyController.Commands.cs
        // už NEMAJÍ [Authorize(Policy="permission:team.manage")] atribut.
        // Důvod: projektId přichází z form body (command.ProjektId nebo query parametr),
        // ne z route. PermissionAuthorizationHandler čte projektId z RouteValues →
        // degradoval by na global-only check. Per-project kontrola probíhá v body
        // přes ExecuteTeamValidatedActionAsync/ExecuteTeamActionAsync.
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.Commands.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:projects.delete\")]",
            "DeleteProject musí mít projects.delete");
        code.Should().Contain("CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId)",
            "team.manage per-project check musí být proveden v body helperech (H-1 IDOR fix)");

        // Meeting akce byly přesunuty do JednaniController — viz docs/known-issues/meetings-endpoints-split-between-controllers.md
        var jednaniCommands = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/JednaniController.Commands.cs"));
        jednaniCommands.Should().Contain("[Authorize(Policy = \"permission:meetings.edit\")]",
            "JednaniController.Delete musí mít meetings.edit");
    }

    [Fact]
    public void ProjektyProjectModals_ShouldUseCorrectPolicies()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.ProjectModals.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:projects.create\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:projects.edit\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:projects.delete\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:team.manage\")]");
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
