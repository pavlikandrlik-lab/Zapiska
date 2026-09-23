using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>Toggle Harmonogram na kartě (2026-07-13): CSS piny — pohled řídí třída na kartě
/// (žádné :has, i15), indikátor vlevo rotuje, akce ve dvou řadách.</summary>
public sealed class RecordScheduleViewCssTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Css => File.ReadAllText(Path.Combine(
        RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));

    [Fact]
    public void ScheduleView_TogglesShells_ByCardClass()
    {
        Css.Should().Contain(".record-card--schedule-view .record-detail-shell");
        Css.Should().Contain(".record-card--schedule-view .record-comments-lazy");
        Css.Should().Contain(".record-card:not(.record-card--schedule-view) .record-schedule-shell");
    }

    [Fact]
    public void ExpandIndicator_MovedLeft_RotationPreserved()
    {
        Css.Should().Contain(".record-expand-indicator");
        Css.Should().Contain(".record-card.collapsed .record-expand-indicator");
        Css.Should().NotContain(".record-toggle-indicator", "stará třída zanikla s markup přesunem");
    }

    [Fact]
    public void RecordBlock_AvoidsFragileModernCss()
    {
        // Úsek record-card sekce: od .record-actions po .record-schedule-shell pravidla.
        var start = Css.IndexOf(".record-actions", StringComparison.Ordinal);
        var end = Css.IndexOf(".record-schedule-shell", start, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        end.Should().BeGreaterThan(start);
        var slice = Css[start..end];
        slice.Should().NotContain(":has(", "i15 pojistka");
        slice.Should().NotContain("max-content", "i15 pojistka");
    }
}
