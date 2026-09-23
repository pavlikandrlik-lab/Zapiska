using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// D1 (2026-07-10): účast na detailu jednání — inline tok karty (jméno·role·e-mail),
/// column-major dvousloupec s deterministickým počtem řádků (--attendance-rows)
/// a decentní CSS číslo řádku. Jen letité konstrukce — i15 lekce
/// (feedback_i15_edge_css_compat): žádné :has / max-content / multi-column.
/// </summary>
public sealed class MeetingAttendanceLayoutTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Css => File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));

    /// <summary>Úsek CSS od attendance gridu po další nesouvisející blok.</summary>
    private static string AttendanceSlice()
    {
        var css = Css;
        var start = css.IndexOf(".meeting-attendance-grid", StringComparison.Ordinal);
        var end = css.IndexOf(".meeting-status", start, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        end.Should().BeGreaterThan(start);
        return css[start..end];
    }

    [Fact]
    public void WideViewport_UsesColumnMajorGrid_WithServerRowCount()
    {
        var slice = AttendanceSlice();
        slice.Should().Contain("grid-auto-flow: column", "sloupce se plní shora dolů");
        slice.Should().Contain("repeat(var(--attendance-rows", "počet řádků určuje server (⌈n/2⌉)");
        Regex.IsMatch(Css, @"@media \(min-width: 1900px\)\s*\{[^@]*grid-auto-flow: column", RegexOptions.Singleline)
            .Should().BeTrue("column-major jen na širokém viewportu (media query, ne container query)");
    }

    [Fact]
    public void AttendanceRow_FlowsInline_WithCounterNumber()
    {
        var slice = AttendanceSlice();
        Regex.IsMatch(slice, @"\.meeting-attendance-person\s*\{[^}]*flex-wrap: wrap", RegexOptions.Singleline)
            .Should().BeTrue("texty v řádce, zalomení až při nedostatku šířky");
        Regex.IsMatch(slice, @"\.meeting-attendance-row::before\s*\{[^}]*counter\(attendance-row\)", RegexOptions.Singleline)
            .Should().BeTrue("decentní číslo řádku přes CSS counter");
        slice.Should().Contain("counter-reset: attendance-row");
    }

    [Fact]
    public void AttendanceBlock_AvoidsFragileModernCss()
    {
        var slice = AttendanceSlice();
        slice.Should().NotContain(":has(", "i15 pojistka — nosná logika bez :has");
        slice.Should().NotContain("max-content", "i15 pojistka — bez intrinsic keywords");
        // Samostatná vlastnost `columns:` (multi-column layout) — grid-*-columns je v pořádku.
        Regex.IsMatch(slice, @"(?:^|[\s;{])columns\s*:", RegexOptions.Multiline)
            .Should().BeFalse("bez multi-column balancování");
    }
}
