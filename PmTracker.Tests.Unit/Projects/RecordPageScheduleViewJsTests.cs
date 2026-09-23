using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// Stránka záznamu (2026-07-14): přepínač Graf ⇄ Tabulka — klientský, stav v třídě
/// kontejneru + localStorage, osy se kreslí až po zviditelnění grafu.
/// </summary>
public sealed class RecordPageScheduleViewJsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Js(string name) => File.ReadAllText(Path.Combine(
        RepoRoot(), "PmTracker.Web/wwwroot/js/modules", name));

    [Fact]
    public void Module_TogglesByContainerClass_AndPersists()
    {
        var src = Js("recordPageScheduleView.js");
        src.Should().Contain("record-page-schedule--table", "režim řídí třída na kontejneru");
        src.Should().Contain("pmtracker.recordPage.scheduleView", "poloha se pamatuje v localStorage");
        src.Should().Contain("aria-pressed", "tlačítka hlásí stav");
    }

    [Fact]
    public void Module_RendersAxesWhenGraphBecomesVisible()
    {
        var src = Js("recordPageScheduleView.js");
        src.Should().Contain("renderStaticTimelineAxes",
            "osy se měří ze šířky — kreslí se až když je graf viditelný");
        src.Should().Contain("queueRainbowSegmentRender");
    }

    [Fact]
    public void Module_IsWiredInBootstrap()
    {
        Js("bootstrap.js").Should().Contain("recordPageScheduleView.js",
            "side-effect modul musí být explicitně importován (memory project_bundle_sync)");
    }
}
