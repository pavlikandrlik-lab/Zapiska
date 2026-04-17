using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Pokrývá specifikaci docs/specs/meetings-year-grouping.md.
/// </summary>
public sealed class MeetingsYearGroupingTests
{
    private static string LoadViewSource(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        var viewPath = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(viewPath).Should().BeTrue($"view soubor musí existovat na cestě {viewPath}");
        return File.ReadAllText(viewPath);
    }

    [Fact]
    public void ApplicationJednaniIndex_ShouldDefaultHistoricalYearsToCollapsed()
    {
        var source = LoadViewSource("PmTracker.Web/Views/Jednani/Index.cshtml");

        source.Should().Contain(
            "data-meeting-history-default=\"collapsed\"",
            "aplikační záložka /Jednani/Index musí sbalovat historické roky (více projektů na stránce)");

        // Aktuální rok je preview, ostatní collapsed
        source.Should().Contain(
            "isPreviewYear ? \"preview\" : \"collapsed\"",
            "aplikační záložka musí nastavit historické roky do stavu collapsed");
    }

    [Fact]
    public void ProjectJednaniTab_ShouldDefaultHistoricalYearsToOpen()
    {
        var source = LoadViewSource("PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml");

        source.Should().Contain(
            "data-meeting-history-default=\"open\"",
            "projektová záložka Jednání musí rozbalit historické roky (jen jeden projekt)");

        // Aktuální rok je preview, ostatní open
        source.Should().Contain(
            "isPreviewYear ? \"preview\" : \"open\"",
            "projektová záložka musí nastavit historické roky do stavu open");
    }

    [Fact]
    public void BothViews_ShouldUseSameMarkupStructure()
    {
        var app = LoadViewSource("PmTracker.Web/Views/Jednani/Index.cshtml");
        var project = LoadViewSource("PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml");

        // Sdílená DOM struktura — spec vyžaduje konzistentní selektory pro JS i CSS
        foreach (var view in new[] { app, project })
        {
            view.Should().Contain("class=\"meeting-year-stack\"");
            view.Should().Contain("data-meeting-overview=\"year-grouped\"");
            view.Should().Contain("data-meeting-year-group");
            view.Should().Contain("data-meeting-year-toggle");
            view.Should().Contain("data-meeting-year-body");
            view.Should().Contain("data-meeting-year-state");
        }
    }

    [Fact]
    public void SpecDocument_ShouldExist()
    {
        // Pokud někdo v budoucnu změní chování, musí být ve specifikaci.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        var specPath = Path.Combine(directory!.FullName, "docs", "specs", "meetings-year-grouping.md");
        File.Exists(specPath).Should().BeTrue(
            $"specifikace musí existovat na {specPath} a popisovat rozdíl mezi aplikační a projektovou záložkou");
    }
}
