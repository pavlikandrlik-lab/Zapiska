using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Spec 2026-09-17 §6.1 — skalární kontrola ScheduleVersion je smazaná.
/// Chránila před kolizí uživatel × automat, která je nedosažitelná (§1.3),
/// a v pilotu blokovala uložení záznamů, které harmonogram vůbec nemění.
/// </summary>
public sealed class ScheduleVersionRemovalTests
{
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relPath)
        => File.ReadAllText(Path.Combine(LocateRepoRoot(), relPath));

    [Theory]
    [InlineData("PmTracker.Web/Services/RecordService.SaveRecord.cs")]
    [InlineData("PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs")]
    [InlineData("PmTracker.Web/Services/ProjectService.ScheduleBlockComposition.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs")]
    [InlineData("PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/form.js")]
    public void ScheduleVersion_NesmiZustatVKodu(string relPath)
    {
        Read(relPath).Should().NotContain("ScheduleVersion",
            $"{relPath} — spec 2026-09-17 §6.1: mechanismus ScheduleVersion je zrušen celý");
    }

    [Fact]
    public void ScheduleStaleData_PravidloNeexistuje()
    {
        Read("PmTracker.Web/Services/RecordService.SaveRecord.cs")
            .Should().NotContain("schedule_stale_data",
                "validační pravidlo schedule_stale_data je zrušeno — generovalo jen falešné konflikty");
    }
}
