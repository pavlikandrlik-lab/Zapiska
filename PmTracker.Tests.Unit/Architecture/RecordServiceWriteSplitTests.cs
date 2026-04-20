using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3C Task 1: RecordService.WriteCommands.cs (1707 LOC) rozdělen
/// do 3 partial souborů (SaveRecord, DeleteRecord, MeetingIdentifier).
/// </summary>
public sealed class RecordServiceWriteSplitTests
{
    [Fact]
    public void OriginalWriteCommandsFile_ShouldBeDeleted()
    {
        File.Exists(ResolvePath("PmTracker.Web/Services/RecordService.WriteCommands.cs"))
            .Should().BeFalse("Fáze 3C Task 1 smazal monolitický soubor");
    }

    [Theory]
    [InlineData("PmTracker.Web/Services/RecordService.SaveRecord.cs")]
    [InlineData("PmTracker.Web/Services/RecordService.DeleteRecord.cs")]
    [InlineData("PmTracker.Web/Services/RecordService.MeetingIdentifier.cs")]
    public void NewPartial_ShouldExistAndDeclarePartialClass(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3C Task 1");
        var content = File.ReadAllText(full);
        content.Should().Contain("partial class RecordService",
            "každý nový soubor deklaruje partial class RecordService (pattern z ProjectService.*, MeetingService.*)");
        content.Should().Contain("namespace PmTracker.Web.Services",
            "namespace konzistentní s ostatními RecordService partials");
    }

    [Fact]
    public void SaveRecordPartial_ShouldContainSaveRecordAsyncOnly()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordService.SaveRecord.cs"));
        content.Should().Contain("public async Task<int> SaveRecordAsync",
            "SaveRecordAsync je v SaveRecord.cs");
        content.Should().NotContain("public async Task DeleteRecordAsync",
            "DeleteRecord patří do DeleteRecord.cs");
        content.Should().NotContain("AssignMeetingIdentifierAsync",
            "MeetingIdentifier patří do MeetingIdentifier.cs");
    }

    [Fact]
    public void DeleteRecordPartial_ShouldContainDeleteRecordAsyncOnly()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordService.DeleteRecord.cs"));
        content.Should().Contain("DeleteRecordAsync");
        content.Should().NotContain("public async Task<int> SaveRecordAsync");
        content.Should().NotContain("AssignMeetingIdentifierAsync");
    }

    [Fact]
    public void MeetingIdentifierPartial_ShouldContainAssignMeetingIdentifier()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordService.MeetingIdentifier.cs"));
        content.Should().Contain("AssignMeetingIdentifierAsync");
    }

    [Fact]
    public void SaveRecordFile_LOCShouldBeReasonable()
    {
        var file = ResolvePath("PmTracker.Web/Services/RecordService.SaveRecord.cs");
        var loc = File.ReadAllLines(file).Length;
        loc.Should().BeLessThan(1600, "SaveRecord může být velký ale < 1600 LOC (obsahuje SaveRecordAsync + všechny validační a harmonogram helpery)");
    }
}
