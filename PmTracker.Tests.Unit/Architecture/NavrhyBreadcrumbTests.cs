using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>A4 (2026-07-08): všechny 4 stránkové akce NavrhyController nastavují breadcrumbs.</summary>
public sealed class NavrhyBreadcrumbTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    [Fact]
    public void AllFourPageActions_SetProjectBreadcrumbs()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/Controllers/NavrhyController.cs"));
        Regex.Matches(src, @"SetProjectBreadcrumbs\(").Count.Should().Be(4,
            "CreateRecordProposal, CreateScheduleProposal, ProposalDetail, PrefillCreateProposal");
        src.Should().Contain("\"Nový návrh záznamu\"");
        src.Should().Contain("\"Návrh změny harmonogramu\"");
        src.Should().Contain("\"Schválení návrhu\"");
        src.Should().Contain("\"Převzetí návrhu\"");
    }
}
