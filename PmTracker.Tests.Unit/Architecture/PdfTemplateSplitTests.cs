using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3A Task 4: PdfTemplate.cshtml (766 LOC) rozdělen na orchestraci
/// + 3 partial views + extracted CSS (pdf-export.css).
/// </summary>
public sealed class PdfTemplateSplitTests
{

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
