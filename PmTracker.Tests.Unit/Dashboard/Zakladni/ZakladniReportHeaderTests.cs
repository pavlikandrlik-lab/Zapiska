using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// 2026-06-30 (revize): hlavička základního reportu = jen název projektu (žádná další
/// identita/počty — ty řeší tematické sekce). Report.cshtml renderuje
/// <c>Model.ProjektNazev</c> jako <c>&lt;h2 class="zr-report-title"&gt;</c>.
/// </summary>
public sealed class ZakladniReportHeaderTests
{
    private static string Read(string rel)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PmTracker.sln"))) d = d.Parent;
        return File.ReadAllText(Path.Combine(d!.FullName, rel.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void Report_renders_project_name_as_title()
    {
        var v = Read("PmTracker.Web/Views/ProjectDashboard/Zakladni/Report.cshtml");
        v.Should().Contain("zr-report-title");
        v.Should().Contain("Model.ProjektNazev");
    }
}
