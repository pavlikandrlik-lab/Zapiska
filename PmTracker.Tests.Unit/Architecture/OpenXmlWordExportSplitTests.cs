using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3A Task 3: OpenXmlWordExportService.cs (1113 LOC) rozdělen do
/// core orchestrator + 2 partial (Metadata, Records) + extracted helper.
/// </summary>
public sealed class OpenXmlWordExportSplitTests
{

    [Theory]
    [InlineData("PmTracker.Web/Services/Export/OpenXmlWordExportService.cs")]
    [InlineData("PmTracker.Web/Services/Export/OpenXmlWordExportService.Metadata.cs")]
    [InlineData("PmTracker.Web/Services/Export/OpenXmlWordExportService.Records.cs")]
    [InlineData("PmTracker.Web/Services/Export/OpenXmlWordElements.cs")]
    public void SplitFile_ShouldExistAndUseExportNamespace(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} existuje po Fázi 3A Task 3");
        File.ReadAllText(full).Should().Contain(
            "namespace PmTracker.Web.Services.Export;",
            "file-scoped namespace konzistentní s projektem");
    }

    [Fact]
    public void CoreFile_ShouldBePartialAndDeclareBuildDocument()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Export/OpenXmlWordExportService.cs"));
        content.Should().Contain("partial class OpenXmlWordExportService",
            "core file musí deklarovat partial — umožňuje rozložení mezi více souborů");
        content.Should().Contain("BuildDocument",
            "public BuildDocument metoda zůstává v core souboru jako orchestrator");
    }

    [Fact]
    public void MetadataPartial_ShouldBePartialAndContainOnlyMetadataHelpers()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Export/OpenXmlWordExportService.Metadata.cs"));
        content.Should().Contain("partial class OpenXmlWordExportService",
            "metadata partial musí deklarovat partial class");
        content.Should().NotContain("public Document BuildDocument", "orchestrator zůstává v core");
    }

    [Fact]
    public void RecordsPartial_ShouldBePartialAndContainOnlyRecordsHelpers()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Export/OpenXmlWordExportService.Records.cs"));
        content.Should().Contain("partial class OpenXmlWordExportService",
            "records partial musí deklarovat partial class");
        content.Should().NotContain("public Document BuildDocument", "orchestrator zůstává v core");
    }

    [Fact]
    public void ElementsHelper_ShouldBeExtractedToOwnFile()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Export/OpenXmlWordElements.cs"));
        content.Should().Contain("class OpenXmlWordElements",
            "OpenXmlWordElements helper má samostatný soubor");
    }

    [Fact]
    public void CoreFile_ShouldBeSignificantlySmaller()
    {
        var file = ResolvePath("PmTracker.Web/Services/Export/OpenXmlWordExportService.cs");
        var loc = File.ReadAllLines(file).Length;
        loc.Should().BeLessThan(400, "core orchestrator je štíhlý — section helpers přesunuty do partials");
    }

    [Fact]
    public void CoreFile_ShouldDeclareSealedPartial()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Export/OpenXmlWordExportService.cs"));
        content.Should().Contain("sealed partial class OpenXmlWordExportService",
            "core soubor deklaruje sealed partial class (ne jen partial — prevent accidental inheritance)");
    }
}
