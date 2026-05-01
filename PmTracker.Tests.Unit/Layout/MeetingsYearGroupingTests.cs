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
        // Index.cshtml nastavuje data-meeting-history-default; year-group stav
        // je v extrahovaném partialu _MeetingYearGroup.cshtml (DRY, úprava #2).
        var source = LoadViewSource("PmTracker.Web/Views/Jednani/Index.cshtml");
        var partial = LoadViewSource("PmTracker.Web/Views/Jednani/_MeetingYearGroup.cshtml");

        source.Should().Contain(
            "data-meeting-history-default=\"collapsed\"",
            "aplikační záložka /Jednani/Index musí sbalovat historické roky (více projektů na stránce)");

        // Aktuální rok je preview, ostatní collapsed — logika je v partialu _MeetingYearGroup
        partial.Should().Contain(
            "IsPreviewYear ? \"preview\" : \"collapsed\"",
            "partial _MeetingYearGroup musí nastavit historické roky do stavu collapsed");
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
        // Po úpravě #2: aplikační záložka extrahovala year-group markup do
        // _MeetingYearGroup.cshtml partialu (DRY). Selektory pro JS/CSS musí
        // existovat v kombinaci Index.cshtml + partial.
        var appIndex = LoadViewSource("PmTracker.Web/Views/Jednani/Index.cshtml");
        var appPartial = LoadViewSource("PmTracker.Web/Views/Jednani/_MeetingYearGroup.cshtml");
        var app = appIndex + appPartial; // kombinace tvoří kompletní markup
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
        // Po úpravě #2: aplikační záložka používá _MeetingYearGroup.cshtml partial.
        var projectView = LoadViewSource("PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml");
        var appIndex = LoadViewSource("PmTracker.Web/Views/Jednani/Index.cshtml");
        var appPartial = LoadViewSource("PmTracker.Web/Views/Jednani/_MeetingYearGroup.cshtml");
        var globalView = appIndex + appPartial; // kombinace tvoří kompletní markup

        foreach (var view in new[] { projectView, globalView })
        {
            // gov-icon může mít atributy v libovolném pořadí (size="s", class, name) — kontrolujeme
            // přítomnost <gov-icon> s class="meeting-year-chevron" a name="chevron-down" zvlášť.
            view.Should().MatchRegex(
                @"<gov-icon[^>]*\bclass=""meeting-year-chevron""[^>]*\bname=""chevron-down""|<gov-icon[^>]*\bname=""chevron-down""[^>]*\bclass=""meeting-year-chevron""",
                "oba views musí používat <gov-icon class=\"meeting-year-chevron\" name=\"chevron-down\"> (ne CSS border hack)");
            view.Should().NotContain(
                "<span class=\"meeting-year-chevron\"",
                "starý span chevron nesmí zůstat — byl nahrazen gov-icon elementem");
        }
    }

    [Fact]
    public void ApplicationJednaniIndex_ShouldWrapHistoricalYearsInProjectHistoryBody()
    {
        // Úprava #2 + sidebar redesign (2026-04-20): historické roky jsou skryté za
        // project-level toggle. Dříve byl toggle na hlavičce; po redesignu je jako
        // <button> v meetings area (button element = implicitní role=button).
        // Markup: [data-project-history-body][hidden] kolem historických year-groups.
        var source = LoadViewSource("PmTracker.Web/Views/Jednani/Index.cshtml");

        source.Should().Contain("data-project-history-toggle",
            "project-level toggle má data atribut pro JS delegaci");
        source.Should().Contain("data-project-history-body",
            "wrapper kolem historických year-groups");
        source.Should().MatchRegex(
            @"<button[^>]*data-project-history-toggle",
            "toggle je button element (implicitní role=button)");
        source.Should().MatchRegex(
            @"data-project-history-body[^>]*hidden",
            "wrapper historických roků je defaultně hidden");
    }

    [Fact]
    public void ProjectHistoryToggleJs_ShouldExportAndHandleToggle()
    {
        var js = LoadViewSource("PmTracker.Web/wwwroot/js/modules/meetingOverview.js");

        js.Should().Contain("export function toggleProjectHistory",
            "meetingOverview.js musí exportovat toggleProjectHistory");
        js.Should().Contain("[data-project-card]",
            "JS používá [data-project-card] selector pro scope");
        js.Should().Contain("[data-project-history-body]",
            "JS musí toggle-ovat [data-project-history-body]");

        var bootstrap = LoadViewSource("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        bootstrap.Should().Contain("toggleProjectHistory",
            "bootstrap.js importuje + volá toggleProjectHistory");
        bootstrap.Should().Contain("[data-project-history-toggle]",
            "bootstrap.js deleguje click na [data-project-history-toggle]");
    }

    [Fact]
    public void ProjectHistoryCss_ShouldStyleHeaderAsToggle()
    {
        // Po sidebar redesign (2026-04-20): toggle je button v meetings area,
        // ne klikatelná header. CSS stylizuje novou třídu.
        var css = LoadViewSource("PmTracker.Web/wwwroot/css/site.css");

        css.Should().Contain(".meeting-project-overview__history-toggle",
            "CSS stylizuje history toggle button v meetings area");
        css.Should().Contain(".meeting-project-chevron",
            "chevron má vlastní class pro rotaci při aria-expanded=true");
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
