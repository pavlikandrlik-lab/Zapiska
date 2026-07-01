using System.IO;
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
    public void Controller_HasZakladniReportAction()
    {
        var c = Read("PmTracker.Web/Controllers/ProjectDashboardController.cs");
        c.Should().Contain("zakladni-report");
        c.Should().Contain("ZakladniReport");
        c.Should().Contain("permission:dashboard.statistics.view");
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
    }

    [Fact]
    public void Selector_HasPresetsAndReloadHook()
    {
        var v = Read("PmTracker.Web/Views/ProjectDashboard/Zakladni/_ObdobiSelector.cshtml");
        v.Should().Contain("data-zakladni-obdobi");
        var js = Read("PmTracker.Web/wwwroot/js/modules/dashboard/zakladniReport.js");
        js.Should().Contain("data-zakladni-obdobi");
        js.Should().Contain("fetch");
    }
}
