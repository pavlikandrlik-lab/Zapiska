using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class ProjektyControllerSplitTests
{
    [Fact]
    public void RootFile_ShouldBePartialClass()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.cs"));
        content.Should().Contain("public sealed partial class ProjektyController",
            "ProjektyController.cs musí být partial class — Fáze 3D Task 1");
    }

    [Theory]
    [InlineData("PmTracker.Web/Controllers/ProjektyController.TabPartials.cs")]
    [InlineData("PmTracker.Web/Controllers/ProjektyController.ProjectModals.cs")]
    [InlineData("PmTracker.Web/Controllers/ProjektyController.Commands.cs")]
    public void NewPartial_ShouldExistAndBePartial(string relativePath)
    {
        File.Exists(ResolvePath(relativePath))
            .Should().BeTrue($"{relativePath} musí existovat — Fáze 3D Task 1");

        var content = File.ReadAllText(ResolvePath(relativePath));
        content.Should().Contain("public sealed partial class ProjektyController",
            $"{relativePath} musí deklarovat public sealed partial class ProjektyController");
    }

    [Fact]
    public void TabPartials_ShouldContainTabEndpoints()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.TabPartials.cs"));
        content.Should().Contain("RecordsTabPartial", "TabPartials musí obsahovat RecordsTabPartial");
        content.Should().Contain("HarmonogramTabPartial", "TabPartials musí obsahovat HarmonogramTabPartial");
        content.Should().Contain("JednaniTabPartial", "TabPartials musí obsahovat JednaniTabPartial");
        content.Should().Contain("TymTabPartial", "TabPartials musí obsahovat TymTabPartial");
        content.Should().Contain("NavrhyTabPartial", "TabPartials musí obsahovat NavrhyTabPartial");
        content.Should().Contain("RecordMeetingCommentStates", "TabPartials musí obsahovat RecordMeetingCommentStates");
        content.Should().Contain("SearchProjectMemberCandidates", "TabPartials musí obsahovat SearchProjectMemberCandidates");
    }

    [Fact]
    public void TabPartials_ShouldNotContainCommandOrModalEndpoints()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.TabPartials.cs"));
        content.Should().NotContain("SaveProject", "TabPartials nesmí obsahovat command endpoint SaveProject");
        content.Should().NotContain("DeleteProject", "TabPartials nesmí obsahovat command endpoint DeleteProject");
        content.Should().NotContain("NewProjectModal", "TabPartials nesmí obsahovat modal endpoint NewProjectModal");
        content.Should().NotContain("EditProjectModal", "TabPartials nesmí obsahovat modal endpoint EditProjectModal");
        content.Should().NotContain("NewMeetingModal", "TabPartials nesmí obsahovat modal endpoint NewMeetingModal");
        content.Should().NotContain("SaveMeeting", "TabPartials nesmí obsahovat command endpoint SaveMeeting");
    }

    [Fact]
    public void ProjectModals_ShouldContainModalEndpoints()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.ProjectModals.cs"));
        content.Should().Contain("NewProjectModal", "ProjectModals musí obsahovat NewProjectModal");
        content.Should().Contain("EditProjectModal", "ProjectModals musí obsahovat EditProjectModal");
        content.Should().Contain("DeleteProjectModal", "ProjectModals musí obsahovat DeleteProjectModal");
        content.Should().Contain("AddTeamMemberModal", "ProjectModals musí obsahovat AddTeamMemberModal");
        content.Should().Contain("AssignProjectRoleModal", "ProjectModals musí obsahovat AssignProjectRoleModal");
        content.Should().Contain("AssignProjectSubsystemModal", "ProjectModals musí obsahovat AssignProjectSubsystemModal");
        content.Should().Contain("AssignProjectSubsystemRoleModal", "ProjectModals musí obsahovat AssignProjectSubsystemRoleModal");
    }

    [Fact]
    public void ProjectModals_ShouldNotContainTabOrCommandEndpoints()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.ProjectModals.cs"));
        content.Should().NotContain("RecordsTabPartial", "ProjectModals nesmí obsahovat tab endpoint RecordsTabPartial");
        content.Should().NotContain("HarmonogramTabPartial", "ProjectModals nesmí obsahovat tab endpoint HarmonogramTabPartial");
        content.Should().NotContain("SaveProject(", "ProjectModals nesmí obsahovat command SaveProject");
        content.Should().NotContain("DeleteProject(", "ProjectModals nesmí obsahovat command DeleteProject");
        content.Should().NotContain("SaveMeeting(", "ProjectModals nesmí obsahovat command SaveMeeting");
    }

    [Fact]
    public void MeetingEndpoints_ShouldLiveInJednaniController_NotProjekty()
    {
        // Meeting modaly a commands přesunuty z ProjektyController do JednaniController
        // 2026-04-23 (viz docs/known-issues/meetings-endpoints-split-between-controllers.md).
        var modalsPath = ResolvePath("PmTracker.Web/Controllers/JednaniController.Modals.cs");
        var commandsPath = ResolvePath("PmTracker.Web/Controllers/JednaniController.Commands.cs");
        File.Exists(modalsPath).Should().BeTrue("JednaniController.Modals.cs musí existovat");
        File.Exists(commandsPath).Should().BeTrue("JednaniController.Commands.cs musí existovat");

        var modalsContent = File.ReadAllText(modalsPath);
        modalsContent.Should().Contain("NewMeetingModal", "Modals musí obsahovat NewMeetingModal");
        modalsContent.Should().Contain("EditMeetingModal", "Modals musí obsahovat EditMeetingModal");

        var commandsContent = File.ReadAllText(commandsPath);
        commandsContent.Should().Contain("IActionResult> Save(", "Commands musí obsahovat Save (dříve SaveMeeting)");
        commandsContent.Should().Contain("IActionResult> Delete(", "Commands musí obsahovat Delete (dříve DeleteMeeting)");

        // ProjektyController staré akce nesmí obsahovat
        File.Exists(ResolvePath("PmTracker.Web/Controllers/ProjektyController.MeetingModals.cs"))
            .Should().BeFalse("ProjektyController.MeetingModals.cs musí být smazán — přesunuto do JednaniController.Modals.cs");
        var projektyCommands = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.Commands.cs"));
        projektyCommands.Should().NotContain("SaveMeeting(", "ProjektyController.Commands.cs nesmí obsahovat SaveMeeting");
        projektyCommands.Should().NotContain("DeleteMeeting(", "ProjektyController.Commands.cs nesmí obsahovat DeleteMeeting");
    }

    [Fact]
    public void Commands_ShouldContainPostActions()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.Commands.cs"));
        content.Should().Contain("SaveProject", "Commands musí obsahovat SaveProject");
        content.Should().Contain("DeleteProject", "Commands musí obsahovat DeleteProject");
        content.Should().Contain("SaveTeamMember", "Commands musí obsahovat SaveTeamMember");
        content.Should().Contain("RemoveTeamMember", "Commands musí obsahovat RemoveTeamMember");
        content.Should().Contain("AssignProjectRole(", "Commands musí obsahovat AssignProjectRole");
        content.Should().Contain("DeactivateProjectRole(", "Commands musí obsahovat DeactivateProjectRole");
        content.Should().Contain("AssignProjectSubsystem(", "Commands musí obsahovat AssignProjectSubsystem");
        content.Should().Contain("ReorderProjectSubsystem", "Commands musí obsahovat ReorderProjectSubsystem");
        content.Should().Contain("DeactivateProjectSubsystem(", "Commands musí obsahovat DeactivateProjectSubsystem");
        content.Should().Contain("AssignProjectSubsystemRole(", "Commands musí obsahovat AssignProjectSubsystemRole");
        content.Should().Contain("DeactivateProjectSubsystemRole(", "Commands musí obsahovat DeactivateProjectSubsystemRole");
    }

    [Fact]
    public void Commands_ShouldNotContainModalOrTabEndpoints()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.Commands.cs"));
        content.Should().NotContain("NewProjectModal", "Commands nesmí obsahovat modal endpoint NewProjectModal");
        content.Should().NotContain("EditProjectModal", "Commands nesmí obsahovat modal endpoint EditProjectModal");
        content.Should().NotContain("RecordsTabPartial", "Commands nesmí obsahovat tab endpoint RecordsTabPartial");
        content.Should().NotContain("HarmonogramTabPartial", "Commands nesmí obsahovat tab endpoint HarmonogramTabPartial");
    }

    [Fact]
    public void RootFile_ShouldContainIndexAndDetailAndSharedHelpers()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.cs"));
        content.Should().Contain("Task<IActionResult> Index", "Root musí obsahovat Index endpoint");
        content.Should().Contain("Task<IActionResult> Detail", "Root musí obsahovat Detail endpoint");
        content.Should().Contain("NormalizeProjectTab", "Root musí obsahovat sdílený helper NormalizeProjectTab");
        content.Should().Contain("PrepareProjectDetailPresentationAsync", "Root musí obsahovat sdílený helper PrepareProjectDetailPresentationAsync");
        content.Should().Contain("BuildProjectStatusOptionsAsync", "Root musí obsahovat sdílený helper BuildProjectStatusOptionsAsync");
        content.Should().Contain("PrepareActiveProjectTabAsync", "Root musí obsahovat sdílený helper PrepareActiveProjectTabAsync");
    }
}
