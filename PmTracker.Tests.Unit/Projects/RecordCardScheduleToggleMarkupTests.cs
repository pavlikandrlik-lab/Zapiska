using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>Toggle Harmonogram na kartě (2026-07-13): markup piny _ZaznamPartial —
/// indikátor rozbalení vlevo (R7), akce max 3 tlačítka + menu + switch (R4-R6, R8),
/// třetí lazy shell pro harmonogram.</summary>
public sealed class RecordCardScheduleToggleMarkupTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Partial => File.ReadAllText(Path.Combine(
        RepoRoot(), "PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml"));

    [Fact]
    public void Header_StartsWithExpandIndicator_OldRightIndicatorGone()
    {
        Regex.IsMatch(Partial, "<header class=\"record-header\"[^>]*>\\s*@\\* R7[\\s\\S]*?<span class=\"record-expand-indicator\"", RegexOptions.Singleline)
            .Should().BeTrue("indikátor rozbalení je první prvek hlavičky (R7)");
        Partial.Should().NotContain("record-toggle-indicator", "stará šipka vpravo je pryč");
    }

    [Fact]
    public void Actions_TwoRows_MenuTriggerWithRotatingChevron_NoCalendarIconsInHeader()
    {
        Partial.Should().Contain("record-actions-row");
        Partial.Should().Contain("data-record-menu-trigger");
        Partial.Should().Contain("data-record-menu");
        // Jedna šipka chevron-right, rotaci na „dolů" řeší CSS podle aria-expanded (robustní bez hidden).
        Partial.Should().Contain("data-record-menu-chevron");
        Partial.Should().Contain("chevron-right");
        Partial.Should().NotContain("chevron-down", "menu má jednu rotující šipku, ne dvě přepínané ikony");
        Partial.Should().Contain("aria-expanded=\"false\"");
        Partial.Should().NotContain("calendar-date", "překlik na záložku se stěhuje do menu");
        Partial.Should().NotContain("calendar-plus", "návrh harmonogramu se stěhuje do menu");
    }

    [Fact]
    public void Menu_HasOpenInNewTabItem_LinkingToRecordPage()
    {
        // Stránka záznamu (2026-07-14): menu karty dostalo vstupní bod na samostatnou stránku.
        Partial.Should().Contain("Otevřít na nové kartě");
        Partial.Should().Contain("Model.RecordPageUrl");
        Regex.IsMatch(Partial, "record-actions-menu-item[\\s\\S]{0,240}target=\"_blank\"")
            .Should().BeTrue("položka otevírá stránku v nové kartě prohlížeče");
        Partial.Should().Contain("rel=\"noopener\"");
    }

    [Fact]
    public void MenuItems_AndSwitch_AreGated()
    {
        Partial.Should().Contain("data-goto-schedule=\"@summary.Id\"", "menu položka drží cross-tab hook");
        Partial.Should().Contain("data-record-view-switch");
        Partial.Should().Contain("data-record-schedule-shell");
        Partial.Should().Contain("data-record-schedule-url");
        // Gating: switch i schedule shell jen s harmonogramem; návrh dle oprávnění.
        Regex.Matches(Partial, @"summary\.MaHarmonogramHodnotu").Count.Should().BeGreaterThanOrEqualTo(3,
            "menu položka + switch + shell jsou gated na MaHarmonogramHodnotu");
        Partial.Should().Contain("summary.CanCreateScheduleProposal");
    }
}
