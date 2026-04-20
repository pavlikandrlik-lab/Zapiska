using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3A Task 4: PdfTemplate.cshtml (766 LOC) rozdělen na orchestraci
/// + 3 partial views + extracted CSS (pdf-export.css).
/// </summary>
public sealed class PdfTemplateSplitTests
{
    private static string RepoRoot()
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

        return directory.FullName;
    }

    private static string ResolvePath(string relative) =>
        Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Orchestration_ShouldBeSlim()
    {
        var file = ResolvePath("PmTracker.Web/Views/Export/PdfTemplate.cshtml");
        File.Exists(file).Should().BeTrue();
        var loc = File.ReadAllLines(file).Length;
        loc.Should().BeLessThan(300, "PdfTemplate.cshtml je orchestrátor — partials + CSS link tvoří většinu obsahu");
    }

    [Fact]
    public void Orchestration_ShouldNotHaveInlineStyleBlock()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Export/PdfTemplate.cshtml"));
        content.Should().NotContain("<style>", "inline CSS přesunut do pdf-export.css");
        content.Should().NotContain("</style>", "inline CSS přesunut do pdf-export.css");
    }

    [Fact]
    public void Orchestration_ShouldLinkPdfExportCss()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Export/PdfTemplate.cshtml"));
        content.Should().Contain("pdf-export.css", "hlavní šablona linkuje extrahovaný CSS soubor");
    }

    [Fact]
    public void Orchestration_ShouldUsePartials()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Export/PdfTemplate.cshtml"));
        content.Should().Contain("Html.PartialAsync", "orchestrátor volá partials místo inline rendering");
    }

    [Theory]
    [InlineData("PmTracker.Web/wwwroot/css/pdf-export.css")]
    [InlineData("PmTracker.Web/Views/Export/_PdfRecordRow.cshtml")]
    [InlineData("PmTracker.Web/Views/Export/_PdfAttendanceBlock.cshtml")]
    [InlineData("PmTracker.Web/Views/Export/_PdfRolesBlock.cshtml")]
    public void NewFile_ShouldExistAndNotBeEmpty(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3A Task 4");
        File.ReadAllText(full).Length.Should().BeGreaterThan(100, "každý partial má smysluplný obsah");
    }

    [Fact]
    public void PdfExportCss_ShouldContainPrintStyles()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/pdf-export.css"));
        // Sanity check: typical print CSS markers
        (content.Contains("@page") || content.Contains("@media print") || content.Contains("body"))
            .Should().BeTrue("pdf-export.css obsahuje alespoň základní print stylování");
    }
}
