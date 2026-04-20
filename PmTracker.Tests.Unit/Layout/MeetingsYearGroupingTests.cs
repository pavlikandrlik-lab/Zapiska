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
    public void ProjectJednaniTab_ShouldDefaultHistoricalYearsToCollapsed()
    {
        var source = LoadViewSource("PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml");

        source.Should().Contain(
            "data-meeting-history-default=\"collapsed\"",
            "projektová záložka Jednání nyní sbalené historické roky (konzistentní s aplikační záložkou)");

        source.Should().Contain(
            "isPreviewYear ? \"preview\" : \"collapsed\"",
            "projektová záložka musí nastavit historické roky do stavu collapsed");
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

    [Fact]
    public void ChevronJs_ShouldSwapIconNameByState()
    {
        // User 2026-04-19 noc: "šipky u jednání jsou pořád blbě" i po CSS rotate
        // opravě. Root cause: gov-icon web component renderuje SVG přes mask/transform,
        // kompozitní CSS rotate vytváří artefakty. Přechod na JS-driven swap
        // `name` atributu (chevron-up / chevron-down) — robustní, nezávislé
        // na interní implementaci gov-icon.
        var js = LoadViewSource("PmTracker.Web/wwwroot/js/modules/meetingOverview.js");

        js.Should().Contain(
            "setMeetingYearChevron(group, \"chevron-down\")",
            "collapsed stav (nebo preview se skrytými) = chevron-down");
        js.Should().Contain(
            "setMeetingYearChevron(group, \"chevron-up\")",
            "open stav (nebo preview bez skrytých) = chevron-up");
        js.Should().Contain(
            "chevron.setAttribute(\"name\", name)",
            "setMeetingYearChevron mění `name` atribut gov-icon");

        // CSS už nesmí mít transform: rotate — swap name dělá JS
        var css = LoadViewSource("PmTracker.Web/wwwroot/css/site.css");
        css.Should().NotMatchRegex(
            @"\.meeting-year-chevron\s*\{[^}]*transform:\s*rotate",
            ".meeting-year-chevron už nepoužívá CSS rotate (gov-icon artefakty)");
    }

    [Fact]
    public void MeetingChevron_ShouldBeGovIconNotCssBorderTrick()
    {
        // User feedback 2026-04-19: původní CSS border-right/border-bottom chevron
        // byl "napíču, blbě" — diagonální chevron uživatel neinterpretoval jako šipku.
        // Přechod na <gov-icon name="chevron-down"> pro intuitivní render.
        var projectView = LoadViewSource("PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml");
        var globalView = LoadViewSource("PmTracker.Web/Views/Jednani/Index.cshtml");

        foreach (var view in new[] { projectView, globalView })
        {
            view.Should().Contain(
                "<gov-icon class=\"meeting-year-chevron\" name=\"chevron-down\"",
                "oba views musí používat gov-icon (ne CSS border hack) pro chevron");
            view.Should().NotContain(
                "<span class=\"meeting-year-chevron\"",
                "starý span chevron nesmí zůstat — byl nahrazen gov-icon elementem");
        }
    }

    [Fact]
    public void MeetingOverviewJs_ShouldImplementSpecToggleBehavior()
    {
        var js = LoadViewSource("PmTracker.Web/wwwroot/js/modules/meetingOverview.js");

        // Toggle přechody dle specu:
        // - open → collapsed
        // - preview + hasHidden → open
        // - preview bez hasHidden → collapsed
        // - collapsed → open
        js.Should().Contain("currentState === \"open\"", "JS musí rozpoznat state 'open' pro přechod do collapsed");
        js.Should().Contain("\"collapsed\"", "JS musí umět nastavit state 'collapsed'");
        js.Should().Contain("currentState === \"preview\"", "JS musí rozpoznat state 'preview'");
        js.Should().Contain("hasHidden", "JS musí rozhodovat podle hasHidden flag");

        // applyMeetingYearState musí nastavit data-meeting-year-has-hidden když má skryté karty
        js.Should().Contain(
            "dataset.meetingYearHasHidden = \"true\"",
            "JS musí označit skupinu atributem data-meeting-year-has-hidden='true' když preview má skryté karty");
    }
}
