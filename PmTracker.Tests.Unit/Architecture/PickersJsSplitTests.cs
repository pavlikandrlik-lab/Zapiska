using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3B Task 3: pickers.js (1530 LOC, 9 exports) rozdělen
/// do 4 feature modulů + re-export barrel + backward-compat barrel.
/// </summary>
public sealed class PickersJsSplitTests
{
    [Theory]
    [InlineData("PmTracker.Web/wwwroot/js/modules/pickers/date.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/pickers/time.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/pickers/person.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/pickers/adPerson.js")]
    public void Submodule_ShouldExistAndNotBeEmpty(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3B Task 3");
        File.ReadAllText(full).Length.Should().BeGreaterThan(300);
    }

    [Fact]
    public void IndexBarrel_ShouldExistAndContainReExports()
    {
        var full = ResolvePath("PmTracker.Web/wwwroot/js/modules/pickers/index.js");
        File.Exists(full).Should().BeTrue("pickers/index.js byl vytvořen v Fázi 3B Task 3");
        File.ReadAllText(full).Length.Should().BeGreaterThan(50, "index barrel není prázdný");
    }

    [Fact]
    public void BarrelModule_ShouldReExport()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/pickers.js"));
        content.Should().Contain("export * from \"./pickers/index.js\"",
            "pickers.js je backward-compat barrel");
        File.ReadAllLines(ResolvePath("PmTracker.Web/wwwroot/js/modules/pickers.js")).Length
            .Should().BeLessThan(50, "barrel je jen re-export");
    }

    [Fact]
    public void DateModule_ShouldContainDatePickerInit()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/pickers/date.js"));
        content.Should().Contain("initCustomDatePickers");
        content.Should().Contain("setAppDateFieldValue");
        content.Should().NotContain("initCustomTimePickers", "time patří do time.js");
        content.Should().NotContain("initSinglePersonPickers", "person patří do person.js");
    }

    [Fact]
    public void TimeModule_ShouldContainTimePickerInit()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/pickers/time.js"));
        content.Should().Contain("initCustomTimePickers");
        content.Should().NotContain("initCustomDatePickers", "date patří do date.js");
    }

    [Fact]
    public void PersonModule_ShouldContainPersonPickerInits()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/pickers/person.js"));
        content.Should().Contain("initSinglePersonPickers");
        content.Should().Contain("initCollabPickers");
        content.Should().Contain("formatPersonEntryLabel");
        content.Should().NotContain("initAdPersonPickers", "AD patří do adPerson.js");
    }

    [Fact]
    public void AdPersonModule_ShouldContainAdPickerInit()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/pickers/adPerson.js"));
        content.Should().Contain("initAdPersonPickers");
    }

    [Fact]
    public void IndexModule_ShouldReExportAll()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/pickers/index.js"));
        content.Should().Contain("export * from \"./date.js\"");
        content.Should().Contain("export * from \"./time.js\"");
        content.Should().Contain("export * from \"./person.js\"");
        content.Should().Contain("export * from \"./adPerson.js\"");
    }

}
