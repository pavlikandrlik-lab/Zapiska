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
/// - respektovat class .comment-pagination-actions--in-header v ASC režimu
/// - být zarovnaná vpravo přes justify-content: flex-end
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

    [Fact]
    public void Css_ShouldAlignPaginationToRight()
    {
        var css = LoadText("PmTracker.Web/wwwroot/css/site.css");

        css.Should().MatchRegex(
            @"\.comment-pagination-actions\s*\{[^}]*justify-content:\s*flex-end",
            "pagination tlačítka jsou vždy vpravo");
    }

    [Fact]
    public void Css_ShouldDefineInHeaderVariantForAscMode()
    {
        var css = LoadText("PmTracker.Web/wwwroot/css/site.css");

        css.Should().Contain(
            ".comment-pagination-actions--in-header",
            "musí existovat CSS třída pro ASC režim (pagination v headeru)");
        css.Should().Contain(
            ".record-comments-header--stacked-right",
            "header v ASC musí mít modifier pro sloupcové zarovnání");
    }

    [Fact]
    public void Js_ShouldPositionPaginationByDirection()
    {
        var js = LoadText("PmTracker.Web/wwwroot/js/modules/comments.js");

        js.Should().Contain(
            "record-comments-header--stacked-right",
            "JS přidává třídu stacked-right na header v ASC");
        js.Should().Contain(
            "comment-pagination-actions--in-header",
            "JS přidává třídu in-header na pagination v ASC");
        // Textová manipulace byla nahrazena CSS-driven visibility — JS už
        // nesetuje textContent na gov-button (duplikovalo obsah).
        js.Should().NotContain(
            "btn.textContent =",
            "JS nesmí setovat textContent na gov-button (gov hydratace duplikuje)");
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
