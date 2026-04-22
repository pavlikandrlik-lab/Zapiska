using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ProjektyAuthzTests
{
    [Fact]
    public void ProjektyCommands_ShouldUsePolicyForTeamManageActions()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.Commands.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:team.manage\")]",
            "team.manage actions musí mít Policy atribut");
        code.Should().Contain("[Authorize(Policy = \"permission:projects.delete\")]",
            "DeleteProject musí mít projects.delete");
        code.Should().Contain("[Authorize(Policy = \"permission:meetings.edit\")]",
            "DeleteMeeting musí mít meetings.edit");
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
    public void ProjektyMeetingModals_ShouldUseCorrectPolicies()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.MeetingModals.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:meetings.create\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:meetings.edit\")]");
    }

    [Fact]
    public void ProjektyCommands_ShouldNotHaveBody_HasPermissionChecks_OnCleanActions()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.Commands.cs"));

        // Counts: SaveProject + SaveMeeting still have body HasPermission (allowed).
        // But team.manage / delete actions should not — body checks must be removed.
        // Simplification: just assert no HasPermission(PermissionKeys.TeamManage,...) body calls remain.
        code.Should().NotContain("CurrentUserContext.HasPermission(PermissionKeys.TeamManage",
            "team.manage body checks musí být nahrazené Policy atributem");
        code.Should().NotContain("CurrentUserContext.HasPermission(PermissionKeys.ProjectsDelete",
            "projects.delete body check musí být nahrazen");
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
