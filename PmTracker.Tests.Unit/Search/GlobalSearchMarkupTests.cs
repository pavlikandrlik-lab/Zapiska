using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Ověřuje strukturu globálního vyhledávání v _Layout.cshtml.
/// Markup využívá skutečné Web Components z @gov-design-system-ce/components
/// hostované LOKÁLNĚ v ~/lib/gov-design-system/ (aplikace MUSÍ běžet offline).
/// </summary>
public sealed class GlobalSearchMarkupTests
{
    private static string LoadLayoutSource()
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

        var layoutPath = Path.Combine(
            directory.FullName,
            "PmTracker.Web",
            "Views",
            "Shared",
            "_Layout.cshtml");

        File.Exists(layoutPath).Should().BeTrue($"_Layout.cshtml musí existovat na cestě {layoutPath}");
        return File.ReadAllText(layoutPath);
    }

    [Fact]
    public void Layout_MaGovFormControl_Tag()
    {
        var layout = LoadLayoutSource();
        layout.Should().Contain("<gov-form-control", "search používá skutečný Web Component gov-form-control");
    }

    [Fact]
    public void Layout_MaGovFormSearch_Tag()
    {
        var layout = LoadLayoutSource();
        layout.Should().Contain("<gov-form-search", "search používá skutečný Web Component gov-form-search");
    }

    [Fact]
    public void Layout_MaGovFormInput_SlotInput()
    {
        var layout = LoadLayoutSource();
        layout.Should().Contain("slot=\"input\"", "gov-form-input musí být ve slotu input dle gov-form-search API");
    }

    [Fact]
    public void Layout_MaGovButton_SlotButton_STextemHledat()
    {
        var layout = LoadLayoutSource();
        layout.Should().Contain("slot=\"button\"", "submit button musí být ve slotu button");
        layout.Should().Contain("Hledat", "submit gov-button musí obsahovat text Hledat");
    }

    [Fact]
    public void Layout_MaGovButton_SlotButtonErase_SIkonouX()
    {
        var layout = LoadLayoutSource();
        layout.Should().Contain("slot=\"button-erase\"", "erase button musí být ve slotu button-erase");
        layout.Should().Contain("name=\"x\"", "erase button musí mít gov-icon s name=x");
    }

    [Fact]
    public void Layout_MaAriaLabel_NaInputu()
    {
        var layout = LoadLayoutSource();
        layout.Should().Contain("aria-label=\"Globální vyhledávání\"", "input pro vyhledávání musí mít přístupný popis");
    }

    [Fact]
    public void Layout_MaLocalScript_GovComponents()
    {
        var layout = LoadLayoutSource();
        layout.Should().Contain("~/lib/gov-design-system/dist/core/core.esm.min.js",
            "layout musí načítat gov-design-system loader LOKÁLNĚ (offline-first, ne z CDN)");
        layout.Should().NotContain("cdn.jsdelivr.net",
            "layout nesmí načítat žádné assety z externí CDN (offline-first)");
    }

    [Fact]
    public void Layout_MaLocalCss_GovComponents()
    {
        var layout = LoadLayoutSource();
        layout.Should().Contain("~/lib/gov-design-system/dist/core/core.min.css",
            "layout musí načítat gov-design-system CSS LOKÁLNĚ");
        layout.Should().Contain("~/lib/gov-design-system/styles/lib/tokens.min.css",
            "layout musí načítat gov-design-system tokens LOKÁLNĚ");
    }

    [Fact]
    public void Layout_MaLocalniGovAssety_VRepozitari()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        var libRoot = Path.Combine(
            directory!.FullName,
            "PmTracker.Web", "wwwroot", "lib", "gov-design-system");

        File.Exists(Path.Combine(libRoot, "dist", "core", "core.esm.min.js"))
            .Should().BeTrue("loader core.esm.min.js musí být lokálně v repozitáři (offline)");
        File.Exists(Path.Combine(libRoot, "dist", "core", "core.min.css"))
            .Should().BeTrue("core.min.css musí být lokálně v repozitáři (offline)");
        File.Exists(Path.Combine(libRoot, "styles", "lib", "tokens.min.css"))
            .Should().BeTrue("tokens.min.css musí být lokálně v repozitáři (offline)");

        // Loader dynamicky importuje chunk p-DT2tslUp.js — musí být vedle něj
        File.Exists(Path.Combine(libRoot, "dist", "core", "p-DT2tslUp.js"))
            .Should().BeTrue("runtime chunk p-DT2tslUp.js musí být lokálně (dynamicky importovaný loaderem)");
    }

    [Fact]
    public void Layout_GlobalSearchJs_NeniPodminenEnabled()
    {
        var layout = LoadLayoutSource();
        var scriptIndex = layout.IndexOf("global-search.js", StringComparison.Ordinal);
        scriptIndex.Should().BeGreaterThan(0, "global-search.js musí být v layoutu");

        // Nesmí být obaleno @if (SearchOptionsAccessor.Value.Enabled)
        var preceding = layout[..scriptIndex];
        var lastIfIndex = preceding.LastIndexOf("@if", StringComparison.Ordinal);
        if (lastIfIndex >= 0)
        {
            var ifBlock = preceding[lastIfIndex..];
            ifBlock.Should().NotContain(
                "SearchOptionsAccessor.Value.Enabled",
                "global-search.js nesmí být podmíněný hodnotou Enabled — má fungovat vždy");
        }
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
