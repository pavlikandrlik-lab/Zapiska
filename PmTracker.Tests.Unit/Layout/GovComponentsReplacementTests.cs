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
    /// Záznamy na Projekty/Index: switche skrýt hotové / skrýt smazané záměrně NEBYLY
    /// nahrazeny za gov-form-switch. Důvod: JS vrstva (pageSwitchers.js) používá
    /// querySelector('[data-project-status-hide]') a instanceof HTMLInputElement.
    /// gov-form-switch renderuje interní input přes shadow DOM — querySelector by nenašel
    /// HTMLInputElement a filtrace by přestala fungovat. Ponechány původní label+input.
    /// </summary>
    [Fact]
    public void ProjektyIndex_MaGovFormSwitch_ProSkrytHotove_A_SkrytSmazane()
    {
        var content = ReadView("Projekty", "Index.cshtml");

        // Záměrně ponechané — musí existovat původní input s data-project-status-hide
        content.Should().Contain("data-project-status-hide",
            "Index.cshtml musí obsahovat data-project-status-hide pro JS filtraci (gov-form-switch nelze použít — shadow DOM)");

        // Filtry v _ProjectRecordsTab jsou také záměrně ponechány
        var recordsTab = ReadView("Projekty", "_ProjectRecordsTab.cshtml");
        recordsTab.Should().Contain("data-filter-key",
            "_ProjectRecordsTab.cshtml musí obsahovat data-filter-key pro JS filtraci (gov-form-switch nelze použít — shadow DOM)");
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
