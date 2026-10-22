using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>Spec 2026-10-22 R9: menu › na kartě — první položka otevře stránku záznamu ve stejné kartě.</summary>
public sealed class RecordCardOpenPageMenuMarkupTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Menu => Regex.Match(
        File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml")),
        "<div class=\"record-actions-menu\"[\\s\\S]*?\\n                        </div>").Value;

    [Fact]
    public void FullScreenItem_IsFirst_AndStaysInSameTab()
    {
        var menu = Menu;
        var full = menu.IndexOf("Otevřít na celou obrazovku", StringComparison.Ordinal);
        full.Should().BeGreaterThan(0);
        full.Should().BeLessThan(menu.IndexOf("Otevřít na nové kartě", StringComparison.Ordinal));
        menu.IndexOf("Otevřít na celou obrazovku", StringComparison.Ordinal)
            .Should().BeLessThan(menu.IndexOf("data-goto-schedule", StringComparison.Ordinal));
        Regex.Match(menu, "<a[^>]*data-record-open-page=\"same-tab\"[^>]*>").Value
            .Should().NotContain("target=", "stejná karta prohlížeče");
    }
}
