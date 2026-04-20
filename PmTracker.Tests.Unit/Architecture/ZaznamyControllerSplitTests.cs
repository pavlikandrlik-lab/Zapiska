using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class ZaznamyControllerSplitTests
{
    [Fact]
    public void RootFile_ShouldBePartialClass()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.cs"));
        content.Should().Contain("public sealed partial class ZaznamyController",
            "ZaznamyController.cs musí být partial class — Fáze 3D Task 2");
    }

    [Theory]
    [InlineData("PmTracker.Web/Controllers/ZaznamyController.Modals.cs")]
    [InlineData("PmTracker.Web/Controllers/ZaznamyController.Partials.cs")]
    [InlineData("PmTracker.Web/Controllers/ZaznamyController.Commands.cs")]
    public void NewPartial_ShouldExistAndBePartial(string relativePath)
    {
        File.Exists(ResolvePath(relativePath))
            .Should().BeTrue($"{relativePath} musí existovat — Fáze 3D Task 2");

        var content = File.ReadAllText(ResolvePath(relativePath));
        content.Should().Contain("public sealed partial class ZaznamyController",
            $"{relativePath} musí deklarovat public sealed partial class ZaznamyController");
    }

    [Fact]
    public void Modals_ShouldContainModalEndpoints()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Modals.cs"));
        content.Should().Contain("DeleteRecordModal", "Modals musí obsahovat DeleteRecordModal");
        content.Should().Contain("AssignMeetingIdentifierModal", "Modals musí obsahovat AssignMeetingIdentifierModal");
    }

    [Fact]
    public void Modals_ShouldNotContainCommandOrPartialEndpoints()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Modals.cs"));
        content.Should().NotContain("Task<IActionResult> Save(", "Modals nesmí obsahovat command endpoint Save");
        content.Should().NotContain("Task<IActionResult> DeleteRecord(", "Modals nesmí obsahovat command endpoint DeleteRecord");
        content.Should().NotContain("Task<IActionResult> RecordCardPartial(", "Modals nesmí obsahovat partial endpoint RecordCardPartial");
        content.Should().NotContain("Task<IActionResult> RecordDetailPartial(", "Modals nesmí obsahovat partial endpoint RecordDetailPartial");
        content.Should().NotContain("Task<IActionResult> AddComment(", "Modals nesmí obsahovat command endpoint AddComment");
    }

    [Fact]
    public void Partials_ShouldContainPartialEndpoints()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Partials.cs"));
        content.Should().Contain("RecordCardPartial", "Partials musí obsahovat RecordCardPartial");
        content.Should().Contain("RecordDetailPartial", "Partials musí obsahovat RecordDetailPartial");
        content.Should().Contain("RecordCommentsPartial", "Partials musí obsahovat RecordCommentsPartial");
    }

    [Fact]
    public void Partials_ShouldNotContainCommandOrModalEndpoints()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Partials.cs"));
        content.Should().NotContain("Task<IActionResult> Save(", "Partials nesmí obsahovat command endpoint Save");
        content.Should().NotContain("Task<IActionResult> DeleteRecord(", "Partials nesmí obsahovat command endpoint DeleteRecord");
        content.Should().NotContain("DeleteRecordModal", "Partials nesmí obsahovat modal endpoint DeleteRecordModal");
        content.Should().NotContain("Task<IActionResult> AddComment(", "Partials nesmí obsahovat command endpoint AddComment");
    }

    [Fact]
    public void Commands_ShouldContainPostEndpoints()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));
        content.Should().Contain("Task<IActionResult> Save(", "Commands musí obsahovat Save");
        content.Should().Contain("Task<IActionResult> DeleteRecord(", "Commands musí obsahovat DeleteRecord");
        content.Should().Contain("AssignMeetingIdentifier(", "Commands musí obsahovat AssignMeetingIdentifier");
        content.Should().Contain("AddComment", "Commands musí obsahovat AddComment");
        content.Should().Contain("UpdateComment", "Commands musí obsahovat UpdateComment");
        content.Should().Contain("DeleteComment", "Commands musí obsahovat DeleteComment");
    }

    [Fact]
    public void Commands_ShouldNotContainModalOrPartialEndpoints()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));
        content.Should().NotContain("Task<IActionResult> DeleteRecordModal(", "Commands nesmí obsahovat modal endpoint DeleteRecordModal");
        content.Should().NotContain("Task<IActionResult> AssignMeetingIdentifierModal(", "Commands nesmí obsahovat modal endpoint AssignMeetingIdentifierModal");
        content.Should().NotContain("Task<IActionResult> RecordCardPartial(", "Commands nesmí obsahovat partial endpoint RecordCardPartial");
        content.Should().NotContain("Task<IActionResult> RecordDetailPartial(", "Commands nesmí obsahovat partial endpoint RecordDetailPartial");
        content.Should().NotContain("Task<IActionResult> RecordCommentsPartial(", "Commands nesmí obsahovat partial endpoint RecordCommentsPartial");
    }

    [Fact]
    public void RootFile_ShouldContainEditorEndpointsAndSharedHelpers()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.cs"));
        content.Should().Contain("Task<IActionResult> Edit(", "Root musí obsahovat Edit endpoint");
        content.Should().Contain("Task<IActionResult> Create(", "Root musí obsahovat Create endpoint");
        content.Should().Contain("NormalizeRecordEditorUiContext", "Root musí obsahovat sdílený helper NormalizeRecordEditorUiContext");
        content.Should().Contain("NormalizeLocalReturnUrl", "Root musí obsahovat sdílený helper NormalizeLocalReturnUrl");
        content.Should().Contain("NormalizeDeleteTab", "Root musí obsahovat sdílený helper NormalizeDeleteTab");
        content.Should().Contain("PrepareRecordEditorModel", "Root musí obsahovat sdílený helper PrepareRecordEditorModel");
    }
}
