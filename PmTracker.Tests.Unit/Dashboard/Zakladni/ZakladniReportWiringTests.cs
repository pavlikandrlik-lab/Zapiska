using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

public sealed class ZakladniReportWiringTests
{
    private static string Read(string rel)
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, rel.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void Controller_StatisticsPanel_vraci_report()
    {
        var c = Read("PmTracker.Web/Controllers/ProjectDashboardController.cs");
        c.Should().NotContain("zakladni-report", "samostatný endpoint se sloučil do statistics-panel (spec §14)");
        c.Should().Contain("Zakladni/Report.cshtml");
        c.Should().Contain("permission:dashboard.statistics.view");
    }

    [Fact]
    public void Grep_zadny_StatisticsPanelViewModel()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        var web = Path.Combine(dir!.FullName, "PmTracker.Web");
        var vyskyty = Directory.EnumerateFiles(web, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".cshtml")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadAllText(f) is var t
                        && (t.Contains("StatisticsPanelViewModel") || t.Contains("BuildStatisticsPanelAsync") || t.Contains("_StatisticsPanel")))
            .Select(f => Path.GetRelativePath(web, f));
        vyskyty.Should().BeEmpty("legacy panel statistik je nahrazený reportem");
    }

    [Fact]
    public void Di_RegistersFrameworkServices()
    {
        var di = Read("PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs");
        di.Should().Contain("IZakladniDatasetLoader");
        di.Should().Contain("ZakladniReportBuilder");
    }

    [Fact]
    public void ReportView_RendersSectionsAndChartPartial()
    {
        var v = Read("PmTracker.Web/Views/ProjectDashboard/Zakladni/Report.cshtml");
        v.Should().Contain("Model.Sections");
        v.Should().Contain("Zakladni/_Chart");
        v.Should().Contain("_ObdobiSelector");
        v.Should().Contain("data-section");
        v.Should().Contain("pd-report__lead", "každá sekce má nadpis a jednovětý popis");
    }

    [Fact]
    public void Selector_HasPresetsAndReloadHook()
    {
        var v = Read("PmTracker.Web/Views/ProjectDashboard/Zakladni/_ObdobiSelector.cshtml");
        v.Should().Contain("data-zakladni-obdobi");
        var js = Read("PmTracker.Web/wwwroot/js/modules/dashboard/zakladniReport.js");
        js.Should().Contain("data-zakladni-obdobi");
        js.Should().Contain("loadDashboardPanel", "znovunačtení jde přes panel dashboardu (asUser, chyba, pm:panel-loaded)");
    }
}
