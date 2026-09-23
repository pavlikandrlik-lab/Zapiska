using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// 2026-09-01: boční indikátor subsystému při rolování ukazuje KÓD subsystému, ne dlouhý
/// název — plné názvy se do úzké bubliny nevejdou. Indikátor je sdílený pro záložku
/// Záznamy i Harmonogram (jedna funkce, aktivní panel → data-subsystem-grouped-shell),
/// takže stačí jeden zdroj popisku. Obě záložky renderují data-subsystem-kod na skupině.
/// </summary>
public sealed class SubsystemRailIndicatorLabelTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Js => Read("PmTracker.Web/wwwroot/js/modules/filters/recordDisplay.js");

    [Fact]
    public void Indicator_UsesSubsystemCode_ForLabel()
    {
        Js.Should().Contain("data-subsystem-kod",
            "popisek bubliny bere kód subsystému (dlouhé názvy se do bubliny nevejdou)");
        Regex.IsMatch(Js, @"label\.textContent\s*=\s*subsystemLabel")
            .Should().BeTrue("do popisku jde vyřešený kód, ne surový název skupiny");
    }

    [Fact]
    public void Indicator_FallsBackToName_WhenCodeMissing()
    {
        // Subsystém bez vyplněného kódu nesmí zůstat s prázdnou bublinou.
        Regex.IsMatch(Js, @"data-subsystem-kod[\s\S]{0,400}data-subsystem-name")
            .Should().BeTrue("chybějící kód spadne zpět na název subsystému");
    }

    [Fact]
    public void BothTabs_RenderSubsystemCode_OnGroup()
    {
        // Indikátor je sdílený → obě záložky musí kód na skupině nabízet.
        Read("PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml")
            .Should().Contain("data-subsystem-kod", "záložka Záznamy");
        Read("PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml")
            .Should().Contain("data-subsystem-kod", "záložka Harmonogram");
    }
}
