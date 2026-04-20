using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3C Task 2: RecordProposalService.cs (1142 LOC) rozdělen
/// do partial class patternu (root + Queries + SubmitCommands + DecisionCommands).
/// </summary>
public sealed class RecordProposalServiceSplitTests
{
    [Fact]
    public void RootFile_ShouldBePartialClass()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordProposalService.cs"));
        content.Should().Contain("partial class RecordProposalService",
            "root soubor musí deklarovat partial class (Fáze 3C Task 2)");
        content.Should().Contain("namespace PmTracker.Web.Services",
            "namespace konzistentní s ostatními partials");
    }

    [Theory]
    [InlineData("PmTracker.Web/Services/RecordProposalService.Queries.cs")]
    [InlineData("PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs")]
    [InlineData("PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs")]
    public void NewPartial_ShouldExistAndBePartial(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3C Task 2");
        var content = File.ReadAllText(full);
        content.Should().Contain("partial class RecordProposalService",
            "každý nový soubor deklaruje partial class RecordProposalService");
        content.Should().Contain("namespace PmTracker.Web.Services",
            "namespace konzistentní s ostatními RecordProposalService partials");
    }

    [Fact]
    public void QueriesPartial_ShouldContainCanViewAndBuildMethods()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordProposalService.Queries.cs"));
        content.Should().Contain("CanViewProposalTabAsync",
            "CanViewProposalTabAsync patří do Queries");
        content.Should().Contain("BuildProjectProposalsTabAsync",
            "BuildProjectProposalsTabAsync patří do Queries");
        content.Should().Contain("BuildCreateRecordProposalEditorAsync",
            "BuildCreateRecordProposalEditorAsync patří do Queries");
        content.Should().Contain("BuildScheduleProposalEditorAsync",
            "BuildScheduleProposalEditorAsync patří do Queries");
        content.Should().Contain("BuildProposalDetailAsync",
            "BuildProposalDetailAsync patří do Queries");
        content.Should().Contain("BuildEditableRecordEditorFromProposalAsync",
            "BuildEditableRecordEditorFromProposalAsync patří do Queries");
        content.Should().Contain("BuildPrefilledCreateRecordEditorFromProposalAsync",
            "BuildPrefilledCreateRecordEditorFromProposalAsync patří do Queries");
    }

    [Fact]
    public void QueriesPartial_ShouldNotContainSubmitOrDecisionMethods()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordProposalService.Queries.cs"));
        content.Should().NotContain("public async Task SubmitCreateRecordProposalAsync",
            "Submit metody patří do SubmitCommands.cs");
        content.Should().NotContain("public async Task SubmitScheduleProposalAsync",
            "Submit metody patří do SubmitCommands.cs");
        content.Should().NotContain("public async Task<int?> ApproveProposalAsync",
            "Decision metody patří do DecisionCommands.cs");
        content.Should().NotContain("public async Task RejectProposalAsync",
            "Decision metody patří do DecisionCommands.cs");
        content.Should().NotContain("public async Task RejectAndTakeOverCreateProposalAsync",
            "Decision metody patří do DecisionCommands.cs");
        content.Should().NotContain("public async Task RejectAndEditProposalAsync",
            "Decision metody patří do DecisionCommands.cs");
    }

    [Fact]
    public void SubmitCommandsPartial_ShouldContainSubmitMethods()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs"));
        content.Should().Contain("SubmitCreateRecordProposalAsync",
            "SubmitCreateRecordProposalAsync patří do SubmitCommands");
        content.Should().Contain("SubmitScheduleProposalAsync",
            "SubmitScheduleProposalAsync patří do SubmitCommands");
    }

    [Fact]
    public void SubmitCommandsPartial_ShouldNotContainQueryOrDecisionMethods()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs"));
        content.Should().NotContain("public async Task<bool> CanViewProposalTabAsync",
            "Query metody patří do Queries.cs");
        content.Should().NotContain("public async Task<ProjektNavrhyTabViewModel> BuildProjectProposalsTabAsync",
            "Query metody patří do Queries.cs");
        content.Should().NotContain("public async Task<int?> ApproveProposalAsync",
            "Decision metody patří do DecisionCommands.cs");
        content.Should().NotContain("public async Task RejectProposalAsync",
            "Decision metody patří do DecisionCommands.cs");
    }

    [Fact]
    public void DecisionCommandsPartial_ShouldContainDecisionMethods()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs"));
        content.Should().Contain("ApproveProposalAsync",
            "ApproveProposalAsync patří do DecisionCommands");
        content.Should().Contain("RejectProposalAsync",
            "RejectProposalAsync patří do DecisionCommands");
        content.Should().Contain("RejectAndTakeOverCreateProposalAsync",
            "RejectAndTakeOverCreateProposalAsync patří do DecisionCommands");
        content.Should().Contain("RejectAndEditProposalAsync",
            "RejectAndEditProposalAsync patří do DecisionCommands");
    }

    [Fact]
    public void DecisionCommandsPartial_ShouldNotContainQueryOrSubmitMethods()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs"));
        content.Should().NotContain("public async Task<bool> CanViewProposalTabAsync",
            "Query metody patří do Queries.cs");
        content.Should().NotContain("public async Task<ProjektNavrhyTabViewModel> BuildProjectProposalsTabAsync",
            "Query metody patří do Queries.cs");
        content.Should().NotContain("public async Task SubmitCreateRecordProposalAsync",
            "Submit metody patří do SubmitCommands.cs");
        content.Should().NotContain("public async Task SubmitScheduleProposalAsync",
            "Submit metody patří do SubmitCommands.cs");
    }
}
