using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Spec 2026-10-24 R11: text stavu harmonogramu skládá jen ScheduleStatusText (C#) a jeho JS
/// zrcadlo schedule/scheduleStatus.js. Kopie textu v šabloně nebo v editoru by se rozešla.
/// </summary>
public sealed class ScheduleStatusTextSourceTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
    }

    [Theory]
    [InlineData("PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml")]
    public void StatusText_is_composed_only_by_the_formatter(string relativePath)
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

        content.Should().NotContain("Aktuální krok:", "text stavu skládá ScheduleStatusText / scheduleStatus.js");
        content.Should().NotContain("skluz +", "text stavu skládá ScheduleStatusText / scheduleStatus.js");
    }
}
