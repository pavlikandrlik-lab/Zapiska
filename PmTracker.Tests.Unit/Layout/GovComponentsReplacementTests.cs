using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Ověřuje, že gov Web Components jsou použity tam, kde byly nahrazeny
/// CSS aproximace, a že záměrně ponechané aproximace mají odůvodnění.
/// </summary>
public sealed class GovComponentsReplacementTests
{
    private static DirectoryInfo RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("repozitář s PmTracker.sln musí být dostupný");
        return directory!;
    }

    private static string ReadView(params string[] pathParts)
    {
        var fullPath = Path.Combine(
            new[] { RepoRoot().FullName, "PmTracker.Web", "Views" }.Concat(pathParts).ToArray());
        File.Exists(fullPath).Should().BeTrue($"soubor {fullPath} musí existovat");
        return File.ReadAllText(fullPath);
    }

    /// <summary>
    /// gov-theme-switch Web Component je použita v _Layout.cshtml.
    /// CSS aproximace (label+input+SVG ikonky) byla odstraněna a nahrazena skutečnou gov komponentou.
    /// theme.js zachycuje gov-change event a řídí 3-stavový model (light/dark/auto).
    /// </summary>
    [Fact]
    public void Layout_MaGovThemeSwitch_NeboPuvodniCssAproximaciSZduvodnenim()
    {
        var content = ReadView("Shared", "_Layout.cshtml");

        // Skutečná gov Web Component musí být přítomna
        content.Should().Contain("<gov-theme-switch",
            "_Layout.cshtml musí obsahovat <gov-theme-switch> Web Component");

        // Původní CSS aproximace musí být odstraněna
        content.Should().NotContain("<span class=\"gov-theme-switch-icon-sun\">",
            "_Layout.cshtml nesmí obsahovat starou CSS aproximaci gov-theme-switch-icon-sun");
        content.Should().NotContain("<span class=\"gov-theme-switch-icon gov-theme-switch-icon-sun\">",
            "_Layout.cshtml nesmí obsahovat starou CSS aproximaci gov-theme-switch-icon");
    }

    /// <summary>
    /// Filtry projektů (Index a _ProjectRecordsTab) jsou nahrazeny za <gov-form-switch>
    /// Web Component. JS vrstva čte property `checked` na komponentě (reflektovaná na atribut)
    /// a poslouchá `gov-change` event místo native `change`. Aproximace `.gov-switch` (label+input
    /// + track + thumb) byla z site.css odstraněna.
    /// </summary>
    [Fact]
    public void ProjektyIndex_PouzivaGovFormSwitch_ProSkrytHotoveASkrytSmazane()
    {
        var content = ReadView("Projekty", "Index.cshtml");

        content.Should().Contain("<gov-form-switch",
            "Index.cshtml musí používat <gov-form-switch> Web Component");
        content.Should().Contain("data-project-status-hide",
            "Index.cshtml musí mít data-project-status-hide na komponentě (light DOM atribut)");
        content.Should().NotContain("class=\"gov-switch\"",
            "Index.cshtml nesmí obsahovat starou .gov-switch CSS aproximaci");

        // 2026-04-30: filter shell extrahován do sdíleného partialu _ProjectFilterShell.cshtml
        // (mountován v Records i Schedule tabech). Spec project-filter-unification-design.
        var filterShell = ReadView("Projekty", "_ProjectFilterShell.cshtml");
        filterShell.Should().Contain("<gov-form-switch",
            "_ProjectFilterShell.cshtml (sdílený partial) musí používat <gov-form-switch>");
        filterShell.Should().Contain("data-filter-key",
            "_ProjectFilterShell.cshtml musí mít data-filter-key na komponentě");
        filterShell.Should().NotContain("class=\"gov-switch",
            "_ProjectFilterShell.cshtml nesmí obsahovat starou .gov-switch CSS aproximaci");

        var recordsTab = ReadView("Projekty", "_ProjectRecordsTab.cshtml");
        recordsTab.Should().Contain("PartialAsync(\"_ProjectFilterShell\"",
            "_ProjectRecordsTab.cshtml musí mountovat sdílený filter shell partial (DRY)");

        var projectModal = ReadView("Projekty", "ProjectModal.cshtml");
        projectModal.Should().Contain("<gov-form-switch",
            "ProjectModal.cshtml musí používat <gov-form-switch>");
        projectModal.Should().NotContain("class=\"gov-switch\"",
            "ProjectModal.cshtml nesmí obsahovat starou .gov-switch CSS aproximaci");
    }

    /// <summary>
    /// site.css nesmí obsahovat CSS aproximaci .gov-switch* — gov-form-switch
    /// je Web Component s vlastním shadow DOM stylingem.
    /// </summary>
    [Fact]
    public void SiteCss_NeobsahujeAproximaciGovSwitch()
    {
        var siteCssPath = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "site.css");
        var content = File.ReadAllText(siteCssPath);

        content.Should().NotMatchRegex(@"^\s*\.gov-switch\s*\{",
            "site.css nesmí obsahovat .gov-switch CSS aproximaci");
        content.Should().NotMatchRegex(@"^\s*\.gov-switch-track\s*\{",
            "site.css nesmí obsahovat .gov-switch-track CSS aproximaci");
        content.Should().NotMatchRegex(@"^\s*\.gov-switch-thumb\s*\{",
            "site.css nesmí obsahovat .gov-switch-thumb CSS aproximaci");
    }

    /// <summary>
    /// Pseudo-třída CSS `:checked` matchuje pouze native form controls
    /// (input[type=checkbox/radio], option). Pro custom elementy jako
    /// &lt;gov-form-switch&gt; nikdy nematchuje, i když má atribut `checked`.
    /// Důsledek: pravidla typu `:has([data-filter-key]:checked)` na
    /// gov-form-switch nikdy neaplikují grouped view, a `:not(:checked)`
    /// se aplikuje VŽDY (custom element není :checked) → flat shell zůstává
    /// viditelný i s zapnutým toggle a nikdo nevidí žádné záznamy.
    /// Místo toho používat atribut selektor `[checked]`, který Stencil
    /// reflektuje na custom elementech.
    /// User report 2026-04-25: "při zapnuté možnosti seskupit dle subsystému
    /// neukáže žádný záznam".
    /// </summary>
    [Fact]
    public void SiteCss_NepouzivaCheckedPseudoClassuNaGovFormSwitchSelektorech()
    {
        var siteCssPath = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "site.css");
        var content = File.ReadAllText(siteCssPath);

        content.Should().NotMatchRegex(
            @"\[data-filter-key=""groupBySubsystem""\]\s*:\s*(?:not\s*\(\s*)?:\s*checked",
            "site.css nesmí používat :checked / :not(:checked) na "
            + "[data-filter-key='groupBySubsystem'] — element je <gov-form-switch> "
            + "(custom element), na který CSS pseudo-třída :checked nematchuje. "
            + "Použij místo toho atribut selektor [checked] / :not([checked]).");
    }

    /// <summary>
    /// _ScheduleBlock.cshtml: alertové bloky jsou nahrazeny gov-message.
    /// </summary>
    [Fact]
    public void ScheduleBlock_AlertsPouzivajiGovMessage()
    {
        var content = ReadView("Shared", "_ScheduleBlock.cshtml");

        content.Should().Contain("<gov-message color=\"warning\">",
            "_ScheduleBlock.cshtml musí používat <gov-message color=\"warning\"> místo .alert.warning");
        content.Should().Contain("<gov-message color=\"primary\">",
            "_ScheduleBlock.cshtml musí používat <gov-message color=\"primary\"> místo .alert.info");
        content.Should().NotContain("<div class=\"alert warning\">",
            "_ScheduleBlock.cshtml nesmí obsahovat staré CSS aprox. .alert.warning");
        content.Should().NotContain("<div class=\"alert info\">",
            "_ScheduleBlock.cshtml nesmí obsahovat staré CSS aprox. .alert.info");
    }

    [Fact]
    public void Layout_GlobalniChyba_PouzivaGovMessage()
    {
        var content = ReadView("Shared", "_Layout.cshtml");

        content.Should().Contain("<gov-message color=\"error\">",
            "_Layout.cshtml musí používat <gov-message color=\"error\"> pro globální chybovou hlášku");
        content.Should().NotContain("class=\"alert alert-error\"",
            "_Layout.cshtml nesmí obsahovat staré .alert.alert-error");
    }

    [Fact]
    public void RecordsPanel_PouzivaGovTag()
    {
        var content = ReadView("ProjectDashboard", "_RecordsPanel.cshtml");

        content.Should().Contain("<gov-tag",
            "_RecordsPanel.cshtml musí používat <gov-tag> místo <span class=\"badge\">");
        content.Should().NotContain("class=\"badge ",
            "_RecordsPanel.cshtml nesmí obsahovat staré badge třídy");
    }

    [Fact]
    public void ProjectScheduleTab_PouzivaGovTag()
    {
        var content = ReadView("Projekty", "_ProjectScheduleTab.cshtml");

        content.Should().Contain("<gov-tag",
            "_ProjectScheduleTab.cshtml musí používat <gov-tag> místo <span class=\"badge schedule-badge-*\">");
        content.Should().NotContain("class=\"badge schedule-badge-",
            "_ProjectScheduleTab.cshtml nesmí obsahovat staré schedule-badge třídy");
    }

    [Fact]
    public void AssignMeetingIdentifierModal_PouzivaGovMessage()
    {
        var content = ReadView("Projekty", "AssignMeetingIdentifierModal.cshtml");

        content.Should().Contain("<gov-message color=\"error\">",
            "AssignMeetingIdentifierModal.cshtml musí používat <gov-message color=\"error\">");
        content.Should().NotContain("class=\"alert danger\"",
            "AssignMeetingIdentifierModal.cshtml nesmí obsahovat .alert.danger");
    }

    [Fact]
    public void SearchIndex_PouzivaGovMessage()
    {
        var content = ReadView("Search", "Index.cshtml");

        content.Should().Contain("<gov-message color=\"primary\">",
            "Search/Index.cshtml musí používat <gov-message> místo .alert.alert-info");
        content.Should().NotContain("class=\"alert alert-info\"",
            "Search/Index.cshtml nesmí obsahovat .alert.alert-info");
    }

    [Fact]
    public void EditZaznamBasicPanel_PouzivaGovMessage()
    {
        var content = ReadView("Projekty", "_EditZaznamBasicPanel.cshtml");

        content.Should().Contain("<gov-message color=\"error\">",
            "_EditZaznamBasicPanel.cshtml musí používat <gov-message color=\"error\"> místo .alert.danger");
        content.Should().Contain("<gov-message color=\"warning\">",
            "_EditZaznamBasicPanel.cshtml musí používat <gov-message color=\"warning\"> místo .alert.warning");
        content.Should().NotContain("class=\"alert danger\"",
            "_EditZaznamBasicPanel.cshtml nesmí obsahovat .alert.danger");
        content.Should().NotContain("class=\"alert warning\"",
            "_EditZaznamBasicPanel.cshtml nesmí obsahovat .alert.warning");
    }
}
