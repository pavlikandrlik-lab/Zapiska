using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// Pokrývá specifikaci docs/specs/record-comments.md.
///
/// Pagination tlačítka (Další / Předchozí / Zobrazit vše) musí
/// - obsahovat dva &lt;span&gt; s data-comment-pagination-label="asc|desc"
///   (CSS-driven visibility podle data-comment-sort-direction na sekci —
///   gov-button hydratace duplikovala obsah při textContent swap,
///   user 2026-04-19 noc: "Další zobrazit předchozí zobrazit vše zobrazit vše")
/// - mít stálé místo v hlavičce vlevo od řazení v obou směrech (spec 2026-10-22 R11;
///   dřív je JS přesouval pod seznam / pod řazení podle směru)
/// </summary>
public sealed class CommentPaginationMarkupTests
{
    private static string LoadText(string relativePath)
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

        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat na cestě {full}");
        return File.ReadAllText(full);
    }

    [Fact]
    public void LoadMoreButton_ShouldHaveDirectionalLabels()
    {
        var view = LoadText("PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml");

        view.Should().MatchRegex(
            @"<span data-comment-pagination-label=""desc"">Další \(@Model\.LoadStep\)</span>",
            "DESC label je span obsahující „Další (N)\"");
        view.Should().MatchRegex(
            @"<span data-comment-pagination-label=""asc"">Předchozí \(@Model\.LoadStep\)</span>",
            "ASC label je span obsahující „Předchozí (N)\" (user preference 2026-04-19 noc: krátký „Předchozí\", ne „Zobrazit předchozí\")");
    }

    [Fact]
    public void LoadAllButton_ShouldShowAllLabel()
    {
        var view = LoadText("PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml");

        view.Should().Contain("data-record-comments-load-all");
        view.Should().MatchRegex(
            @"data-record-comments-load-all\b[\s\S]*?Zobrazit vše[\s\S]*?</pm-button>",
            "load-all tlačítko obsahuje text „Zobrazit vše\" (stejný pro ASC i DESC)");
    }

    /// <summary>
    /// Uživatel 2026-10-08: Další / Předchozí / Zobrazit vše byly Ghost (neutral base) a špatně
    /// viditelné — zůstávají neutrální, ale s ohraničením (GhostOutlined = neutral outlined).
    /// </summary>
    [Fact]
    public void PaginationButtons_AreGhostOutlined()
    {
        var view = LoadText("PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml");

        view.Should().MatchRegex(@"<pm-button variant=""GhostOutlined""\s+size=""Small""\s+data-record-comments-load-more=""true""");
        view.Should().MatchRegex(@"<pm-button variant=""GhostOutlined""\s+size=""Small""\s+data-record-comments-load-all=""true""");
    }

    [Fact]
    public void View_RendersPaginationInHeader_BeforeSortToggle()
    {
        var view = LoadText("PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml");

        var header = view.IndexOf("class=\"record-comments-header\"", StringComparison.Ordinal);
        var tools = view.IndexOf("class=\"record-comments-tools\"", StringComparison.Ordinal);
        var pagination = view.IndexOf("class=\"comment-pagination-actions\"", StringComparison.Ordinal);
        var sortToggle = view.IndexOf("data-comment-sort-toggle", StringComparison.Ordinal);
        var list = view.IndexOf("data-comment-list", StringComparison.Ordinal);

        header.Should().BeGreaterThan(-1);
        tools.Should().BeGreaterThan(header);
        pagination.Should().BeGreaterThan(tools).And.BeLessThan(sortToggle, "stránkování vlevo od řazení (R11)");
        sortToggle.Should().BeLessThan(list, "nástroje jsou v hlavičce nad seznamem");
        view.IndexOf("comment-pagination-actions", list, StringComparison.Ordinal).Should().Be(-1, "pod seznamem žádná tlačítka");
    }

    [Fact]
    public void Css_HeaderToolsWrapAndAlignRight()
    {
        var css = LoadText("PmTracker.Web/wwwroot/css/site.css");

        css.Should().MatchRegex(@"\.record-comments-header\s*\{[^}]*flex-wrap:\s*wrap", "na úzkém okně se nástroje zalomí pod nadpis");
        css.Should().MatchRegex(@"\.record-comments-tools\s*\{[^}]*margin-left:\s*auto", "nástroje drží vpravo");
        css.Should().MatchRegex(@"\.record-comments-tools\s*\{[^}]*flex-wrap:\s*wrap");
    }

    [Fact]
    public void PaginationIsNotMovedByDirection()
    {
        var css = LoadText("PmTracker.Web/wwwroot/css/site.css");
        var js = LoadText("PmTracker.Web/wwwroot/js/modules/comments.js");

        css.Should().NotContain("comment-pagination-actions--in-header");
        css.Should().NotContain("record-comments-header--stacked-right");
        js.Should().NotContain("comment-pagination-actions--in-header", "tlačítka mají stálé místo (R11)");
        js.Should().NotContain("record-comments-header--stacked-right");
        js.Should().NotContain("appendChild(paginationActions)");
        // Textová manipulace byla nahrazena CSS-driven visibility — JS nesetuje
        // textContent na gov-button (duplikovalo obsah).
        js.Should().NotContain("btn.textContent =", "JS nesmí setovat textContent na gov-button (gov hydratace duplikuje)");
    }

    [Fact]
    public void Css_ShouldHideInactivePaginationLabel()
    {
        var css = LoadText("PmTracker.Web/wwwroot/css/site.css");

        css.Should().Contain(
            "[data-comment-pagination-label=\"desc\"]",
            "CSS cílí neaktivní DESC label v ASC módu");
        css.Should().Contain(
            "[data-comment-pagination-label=\"asc\"]",
            "CSS cílí neaktivní ASC label v DESC módu");
    }

    [Fact]
    public void SpecDocument_ShouldExist()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        var specPath = Path.Combine(directory!.FullName, "docs", "specs", "record-comments.md");
        File.Exists(specPath).Should().BeTrue($"specifikace musí existovat na {specPath}");
    }
}
