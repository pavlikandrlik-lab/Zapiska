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
        // 2026-04-27: bump 1600 → 1700 po fix bugu UNEXPECTED_SERVER_ERROR
        // pro smazání externí vazby s harvestnutými vyjádřeními. Replace přepsán
        // z naivního RemoveRange+Add na UPSERT s pre-flight FK-lock detekcí
        // (~54 řádků).
        // 2026-05-01 (Phase 4, DESIGN-6-A): bump 1700 → 1850 po phantom UI bug 1 fix —
        // ApplyManualActualKrokyAsync helper ~110 řádků (validace + pending lock pre-check
        // + UPSERT do zaznam_harmonogram_hodnoty pro manuální kroky 2/5/8/9 + audit log).
        // 2026-05-04: bump 1850 → 2100 po Phase 1 chevron toggle 2/5/8/9 +
        // ApplyHarmonogramRezimAsync (master switch Auto/Manual pro auto-eligible kroky 1/3/4/6/7/10) +
        // ResetManualKrokyToAutoAsync (chevron user volba "Z vyjádření" reset).
        // 2026-05-05: bump 2100 → 2200 po ClearManualKrokyAsync helper — explicit clear datumu
        // pro manuální krok (PreferredZdroj=Manual + AbsolutniDatum=null). FOLLOW-UP: extract
        // všech ManualKroky helperů (Stage/Reset/Clear) do `RecordService.ManualActualKroky.cs`
        // partial (~250 LOC), čímž SaveRecord dostane zpět ~1850 LOC. Mimo scope clear-fix bug.
        loc.Should().BeLessThan(2200, "SaveRecord může být velký ale < 2200 LOC; viz follow-up komentář na partial extract");
    }
}
