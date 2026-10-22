using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Spec 2026-10-22 R13: ✕ u plánu se zamyká spolu s kalendářem (form.js při změně kategorie,
/// pm-date-field při změně atributu locked). Přepočet trvání běží v datum-modelu — dřív hledal
/// zrušený skrytý vstup data-schedule-duration-hidden a nedělal nic.
/// </summary>
public sealed class PlanDateClearSourceTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(RepoRoot(), relative));

    [Fact]
    public void FormJs_ClearButtonFollowsCalendarLock()
        => Regex.IsMatch(
                Read("PmTracker.Web/wwwroot/js/modules/recordEditor/form.js"),
                @"setScheduleDateFieldState = \(\) => \{[\s\S]*?\[data-app-date-clear\][\s\S]*?clearButton\.disabled = shouldDisable")
            .Should().BeTrue("✕ se odemyká a zamyká s kalendářem (např. přepnutí kategorie na Úkol)");

    [Fact]
    public void PmDateField_LockedAttributeChange_LocksClearButton()
        => Regex.IsMatch(
                Read("PmTracker.Web/wwwroot/js/components/pm-date-field.js"),
                @"case ATTR_LOCKED: \{[\s\S]*?\[data-app-date-clear\][\s\S]*?break;")
            .Should().BeTrue();

    [Fact]
    public void DurationBinding_UsesDatumModel()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/harmonogram/duration-calendar-binding.js");
        src.Should().Contain("from \"./planDuration.js\"");
        src.Should().NotContain("data-schedule-duration-hidden", "skrytý vstup trvání datum-model zrušil");
    }
}
