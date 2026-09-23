using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>B7 (2026-07-09): margin-top:auto odvezl akce na dno natažené karty (rozbalené roky).</summary>
public sealed class MeetingOverviewActionsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    [Fact]
    public void OverviewActions_DoNotUseAutoTopMargin()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));
        var block = Regex.Match(css, @"\.meeting-project-overview__actions\s*\{[^}]*\}", RegexOptions.Singleline);
        block.Success.Should().BeTrue();
        block.Value.Should().NotContain("margin-top: auto",
            "auto-margin posílá tlačítka na dno karty natažené rozbalenými roky");
    }
}
