using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// Pokrývá specifikaci docs/specs/record-comments.md.
///
/// Pagination tlačítka (Další / Zobrazit předchozí / Zobrazit vše) musí
/// - mít oba data-label-desc a data-label-asc atributy (JS přepíná texty)
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
            @"data-label-desc=""Další \(\d+\)""|data-label-desc=""Další \(@Model\.LoadStep\)""",
            "DESC label obsahuje počet záznamů „Další (N)\"");
        view.Should().Contain(
            "data-label-asc=\"Zobrazit předchozí",
            "v ASC se načtou předchozí (starší) vyjádření nahoře");
    }

    [Fact]
    public void LoadAllButton_ShouldHaveDirectionalLabels()
    {
        var view = LoadText("PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml");

        // Obě varianty mají text "Zobrazit vše" — sémanticky stejné
        view.Should().Contain("data-record-comments-load-all");
        view.Should().MatchRegex(
            @"data-record-comments-load-all\b[\s\S]*?data-label-desc=""Zobrazit vše""",
            "load-all tlačítko má data-label-desc=Zobrazit vše");
        view.Should().MatchRegex(
            @"data-record-comments-load-all\b[\s\S]*?data-label-asc=""Zobrazit vše""",
            "load-all tlačítko má data-label-asc=Zobrazit vše");
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
    public void Js_ShouldSwitchLabelsAndPositionByDirection()
    {
        var js = LoadText("PmTracker.Web/wwwroot/js/modules/comments.js");

        js.Should().Contain(
            "data-label-asc",
            "JS čte ASC label z atributu");
        js.Should().Contain(
            "data-label-desc",
            "JS čte DESC label z atributu");
        js.Should().Contain(
            "record-comments-header--stacked-right",
            "JS přidává třídu stacked-right na header v ASC");
        js.Should().Contain(
            "comment-pagination-actions--in-header",
            "JS přidává třídu in-header na pagination v ASC");
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
