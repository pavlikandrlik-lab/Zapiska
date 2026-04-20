using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class ProjektyViewModelsSplitTests
{
    [Fact]
    public void OriginalMonolithFile_ShouldBeDeleted()
    {
        File.Exists(ResolvePath("PmTracker.Web/Models/ViewModels/ProjektyViewModels.cs"))
            .Should().BeFalse("Fáze 3D Task 3 smazal monolitický ProjektyViewModels.cs, obsah rozdělen do Projekty/ subfolder");
    }

    [Theory]
    [InlineData("PmTracker.Web/Models/ViewModels/Projekty/ProjektListViewModels.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Projekty/ProjektDetailViewModels.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Projekty/ProjektJednaniTabViewModels.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Projekty/ProjektTymTabViewModels.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs")]
    public void NewSplitFile_ShouldExistWithCorrectNamespace(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"soubor {relativePath} musí existovat po Fáze 3D Task 3 splitu");
        var content = File.ReadAllText(full);
        content.Should().Contain("namespace PmTracker.Web.Models.ViewModels;",
            "namespace musí být zachován (bez subfolder reflection) — zero consumer breakage");
    }

    [Fact]
    public void NoSplitFile_ShouldContainViewModelsFromOtherConcerns()
    {
        // Verify isolation: list file should not contain ProjektDetailViewModel
        var listContent = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Projekty/ProjektListViewModels.cs"));
        listContent.Should().NotContain("class ProjektDetailViewModel",
            "ProjektListViewModels.cs nesmí obsahovat ProjektDetailViewModel");

        // Verify isolation: detail file should not contain ProjektListViewModel
        var detailContent = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/Projekty/ProjektDetailViewModels.cs"));
        detailContent.Should().NotContain("class ProjektListViewModel",
            "ProjektDetailViewModels.cs nesmí obsahovat ProjektListViewModel");
    }

    [Fact]
    public void AllOriginalTypes_ShouldBeFoundInSplitFiles()
    {
        var splitDir = ResolvePath("PmTracker.Web/Models/ViewModels/Projekty");
        var allContent = string.Concat(
            Directory.GetFiles(splitDir, "*.cs").Select(File.ReadAllText));

        var expectedTypes = new[]
        {
            "class ProjektyIndexViewModel",
            "class ProjektListItemViewModel",
            "class ProjektDetailViewModel",
            "class ProjektHeaderViewModel",
            "class ProjektLazyTabShellViewModel",
            "class ProjektZaznamyTabViewModel",
            "class ProjektZaznamGroupViewModel",
            "class ProjektZaznamCardShellViewModel",
            "class ZaznamCardSummaryViewModel",
            "class ZaznamCardDetailViewModel",
            "class ZaznamCommentsPanelViewModel",
            "class HarmonogramBlockViewModel",
            "class ProjektHarmonogramTabViewModel",
            "class ProjektJednaniTabViewModel",
            "class ProjektTymTabViewModel",
            "class SubsystemGroupViewModel",
            "class ZaznamCardViewModel",
            "class ProjektHarmonogramUkolViewModel",
            "class ExterniOdkazViewModel",
            "class VyjadreniViewModel",
            "class ProjectMemberCandidateViewModel",
            "class ProjectRoleAssignmentViewModel",
            "class ProjectRoleGridRowViewModel",
            "class ProjectRoleHistoryGridRowViewModel",
            "class ProjectRoleHistoryItemViewModel",
            "class ProjectSubsystemViewModel",
            "class ProjectTeamSubsystemRowViewModel",
            "class ProjectSubsystemOptionViewModel",
            "class ProjectSubsystemRoleAssignmentViewModel",
            "class ProjectSubsystemRoleHistoryItemViewModel",
            "class TeamMemberViewModel",
            "class TeamCandidateViewModel",
            "class JednaniListItemViewModel",
            "class JednaniOptionViewModel",
            "class ZaznamEditViewModel",
            "class HarmonogramKrokEditViewModel",
            "class HarmonogramSouhrnViewModel",
            "class SubsystemOptionViewModel",
            "class ProjektFiltryViewModel",
            "class ProjektMeetingCommentStatesResponseViewModel",
            "class ExterniOdkazEditViewModel",
            "class SpolupracovnikViewModel",
            "class SpolupracovnikOptionViewModel",
        };

        foreach (var typeName in expectedTypes)
        {
            allContent.Should().Contain(typeName,
                $"typ '{typeName}' musí existovat v některém ze split souborů");
        }
    }
}
