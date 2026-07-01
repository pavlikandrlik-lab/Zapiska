using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class ScheduleMarkerCssTests
{
    private static string Css()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "PmTracker.Web", "wwwroot", "css", "site.css"));
    }

    [Fact]
    public void AxisEventLabel_IsPositionedBelowMonthLabels()
    {
        var css = Css();
        css.Should().Contain(".timeline-axis-event-label {",
            "DNES/TERMÍN popisky na ose musí mít CSS definici");
        css.Should().Contain("top: 50%",
            "popisky DNES/TERMÍN sedí v dolní polovině osy (pod měsíčními popisky)");
    }
}
