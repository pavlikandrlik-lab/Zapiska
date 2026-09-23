using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>Toggle Harmonogram na kartě (2026-07-13): JS piny — menu s mousedown-origin
/// guardem na floating vrstvě + view-switch modul (lazy-load, persistence, reset).</summary>
public sealed class RecordScheduleToggleJsTests
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

    /// <summary>
    /// 2026-09-03: odkaz „odkud skutečnost pochází" se v read-only nabídne jen tomu, kdo smí
    /// okno vyjádření otevřít — jinak by ikona vedla na akci, kterou server odmítne.
    /// </summary>
    [Fact]
    public void SourceVyjadreniLink_IsGatedByModalOpenPermission()
    {
        var controller = File.ReadAllText(Path.Combine(
            RepoRoot(), "PmTracker.Web/Controllers/ZaznamyController.cs"));
        controller.Should().Contain("PermissionKeys.VyjadreniModalOpen",
            "stránka záznamu se ptá na oprávnění otevřít okno vyjádření");
        controller.Should().Contain("BuildRecordDetailPageAsync(projektId.Value, id, canOpenVyjadreni",
            "a předává ho do skládání stránky");

        var composition = File.ReadAllText(Path.Combine(
            RepoRoot(), "PmTracker.Web/Services/ProjectService.RecordDetailPage.cs"));
        composition.Should().Contain("AttachSourceVyjadreniAsync",
            "zdrojová vazba se ke krokům doplňuje jen v této větvi");
        System.Text.RegularExpressions.Regex.IsMatch(
            composition, @"canOpenVyjadreni\s*\?\s*await AttachSourceVyjadreniAsync", System.Text.RegularExpressions.RegexOptions.Singleline)
            .Should().BeTrue("bez oprávnění se zdrojová vazba ke krokům vůbec nedoplní");
    }

    [Fact]
    public void Menu_UsesFloatingLayer_WithMousedownOriginGuard()
    {
        // 2026-09-07: menu karty i menu přesunu PNF sdílí jeden modul ui/anchoredMenu.js.
        var src = Js("ui/anchoredMenu.js");
        src.Should().Contain("mountFloatingPanel", "menu jede na sdílené floating vrstvě");
        src.Should().Contain("closeAllFloatingPanels");
        src.Should().Contain("mousedown", "outside-close vyhodnocuje původ gesta, ne click (drag lekce z modalů)");
        src.Should().Contain("Escape");
    }

    [Fact]
    public void Menu_IsWiredInBootstrap()
    {
        Js("bootstrap.js").Should().Contain("ui/anchoredMenu.js",
            "side-effect modul musí být explicitně importován (memory project_bundle_sync)");
    }

    [Fact]
    public void ScheduleView_RendersAxesAfterShow_AndListensGovChange()
    {
        var src = Js("recordScheduleView.js");
        src.Should().Contain("renderStaticTimelineAxes", "osy se kreslí až po zviditelnění (vzor Rozpad)");
        src.Should().Contain("queueRainbowSegmentRender");
        src.Should().Contain("recordViewSchedule", "dataset je jediný zdroj pravdy stavu");
        src.Should().Contain("gov-change", "gov-form-switch emituje gov-change i change");
        src.Should().Contain("record-card--schedule-view");
        src.Should().Contain("data-record-schedule-retry");
    }

    [Fact]
    public void Persistence_CoversCardRefresh_AndPanelRestore()
    {
        var refresh = Js("recordRefresh.js");
        refresh.Should().Contain("scheduleViewRecordIds", "preserve celého panelu (bfcache/pageshow) drží pohled");
        refresh.Should().Contain("loadRecordSchedule");
        refresh.Should().Contain("applyRecordViewState");

        var crossNav = Js("crossTabNav.js");
        crossNav.Should().Contain("resetRecordViewToRecord",
            "goto-record z harmonogramu vrací kartu do pohledu záznam (R9/4)");

        Js("bootstrap.js").Should().Contain("recordScheduleView.js");
    }
}
