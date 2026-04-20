using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class HarmonogramServiceSplitTests
{
    [Fact]
    public void NewCatalogServiceFile_ShouldExist()
    {
        File.Exists(ResolvePath("PmTracker.Web/Services/Data/HarmonogramCatalogService.cs"))
            .Should().BeTrue("Fáze 3C Task 3 vytvořil HarmonogramCatalogService.cs pro catalog doménu");
    }

    [Fact]
    public void NewCatalogInterfaceFile_ShouldExist()
    {
        File.Exists(ResolvePath("PmTracker.Web/Services/Data/IHarmonogramCatalogService.cs"))
            .Should().BeTrue("Fáze 3C Task 3 vytvořil IHarmonogramCatalogService.cs");
    }

    [Fact]
    public void CatalogInterface_ShouldDeclareCatalogMethodsOnly()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/IHarmonogramCatalogService.cs"));
        content.Should().Contain("BuildCiselnikDetailAsync", "IHarmonogramCatalogService musí deklarovat BuildCiselnikDetailAsync");
        content.Should().Contain("BuildHarmonogramKrokyCiselnikDetailAsync", "IHarmonogramCatalogService musí deklarovat BuildHarmonogramKrokyCiselnikDetailAsync");
        content.Should().Contain("CountHarmonogramCatalogRowsAsync", "IHarmonogramCatalogService musí deklarovat CountHarmonogramCatalogRowsAsync");
        content.Should().Contain("SaveHarmonogramStepRowAsync", "IHarmonogramCatalogService musí deklarovat SaveHarmonogramStepRowAsync");
        content.Should().Contain("DeleteHarmonogramStepRowAsync", "IHarmonogramCatalogService musí deklarovat DeleteHarmonogramStepRowAsync");
    }

    [Fact]
    public void CatalogInterface_ShouldNotDeclareScheduleMethods()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/IHarmonogramCatalogService.cs"));
        content.Should().NotContain("GetActiveHarmonogramSchemaAsync", "IHarmonogramCatalogService nesmí obsahovat schedule metody");
        content.Should().NotContain("BuildHarmonogramVypocetPublic", "IHarmonogramCatalogService nesmí obsahovat schedule metody");
        content.Should().NotContain("EnsurePersistedActiveHarmonogramSchemaVersionAsync", "IHarmonogramCatalogService nesmí obsahovat schedule metody");
    }

    [Fact]
    public void ScheduleInterface_ShouldNotDeclareCatalogMethods()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/IHarmonogramService.cs"));
        content.Should().NotContain("BuildCiselnikDetailAsync", "IHarmonogramService nesmí obsahovat catalog metody");
        content.Should().NotContain("SaveHarmonogramStepRowAsync", "IHarmonogramService nesmí obsahovat catalog metody");
        content.Should().NotContain("DeleteHarmonogramStepRowAsync", "IHarmonogramService nesmí obsahovat catalog metody");
        content.Should().NotContain("BuildHarmonogramKrokyCiselnikDetailAsync", "IHarmonogramService nesmí obsahovat catalog metody");
        content.Should().NotContain("CountHarmonogramCatalogRowsAsync", "IHarmonogramService nesmí obsahovat catalog metody");
    }

    [Fact]
    public void ScheduleService_ShouldNotContainCatalogMethods()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/HarmonogramService.cs"));
        content.Should().NotContain("SaveHarmonogramStepRowAsync", "HarmonogramService nesmí obsahovat catalog metody — přesunuty do HarmonogramCatalogService");
        content.Should().NotContain("DeleteHarmonogramStepRowAsync", "HarmonogramService nesmí obsahovat catalog metody — přesunuty do HarmonogramCatalogService");
        content.Should().NotContain("CloneActiveHarmonogramSchemaAsync", "CloneActiveHarmonogramSchemaAsync je catalog helper — přesunut do HarmonogramCatalogService");
    }

    [Fact]
    public void CatalogService_ShouldImplementCatalogInterface()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/HarmonogramCatalogService.cs"));
        content.Should().Contain("IHarmonogramCatalogService", "HarmonogramCatalogService musí implementovat IHarmonogramCatalogService");
    }

    [Fact]
    public void CatalogService_ShouldNotImplementScheduleInterface()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/HarmonogramCatalogService.cs"));
        content.Should().NotContain("IHarmonogramService", "HarmonogramCatalogService nesmí implementovat IHarmonogramService — jinak by byl dual-purpose");
    }

    [Fact]
    public void DictionaryService_ShouldDependOnCatalogInterface()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dictionaries/DictionaryService.cs"));
        content.Should().Contain("IHarmonogramCatalogService", "DictionaryService musí injektovat IHarmonogramCatalogService (catalog metody)");
        content.Should().NotContain("IHarmonogramService", "DictionaryService nesmí již záviset na schedule-only IHarmonogramService");
    }
}
