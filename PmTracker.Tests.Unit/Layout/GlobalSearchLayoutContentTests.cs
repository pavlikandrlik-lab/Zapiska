using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Layout;

public sealed class GlobalSearchLayoutContentTests
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
    public void Layout_ShouldContainGlobalSearchForm_Unconditionally()
    {
        var layout = LoadLayoutSource();

        layout.Should().Contain("data-global-search", "formulář vyhledávání musí být v layoutu");
        layout.Should().Contain("role=\"search\"", "formulář musí mít ARIA roli search");

        // Nesmí být zabalený v @if (SearchOptionsAccessor.Value.Enabled) {...}
        var formIndex = layout.IndexOf("data-global-search", StringComparison.Ordinal);
        formIndex.Should().BeGreaterThan(0);

        // Najdeme nejbližší if nad formem a ověříme, že neobsahuje SearchOptionsAccessor.Value.Enabled
        var precedingSection = layout[..formIndex];
        var lastIfIndex = precedingSection.LastIndexOf("@if", StringComparison.Ordinal);

        if (lastIfIndex > 0)
        {
            var ifSnippet = precedingSection[lastIfIndex..];
            ifSnippet.Should().NotContain(
                "SearchOptionsAccessor.Value.Enabled",
                "vyhledávací formulář nesmí být podmíněný hodnotou Enabled — má být vidět vždy");
        }
    }

    [Fact]
    public void Layout_ShouldContainSearchInputAndSubmitButton()
    {
        var layout = LoadLayoutSource();

        // Struktura využívá skutečné Web Components z @gov-design-system-ce/components
        layout.Should().Contain("<gov-form-search", "wrapper musí být skutečný gov-form-search Web Component");
        layout.Should().Contain("slot=\"input\"", "vstupní pole je umístěno ve slotu input");
        layout.Should().Contain("slot=\"button\"", "tlačítko je umístěno ve slotu button");

        layout.Should().Contain("name=\"q\"", "input musí mít jméno q");

        layout.Should().Contain("<gov-button", "submit musí být gov-button Web Component");
        layout.Should().Contain("Hledat", "submit button musí obsahovat text Hledat");
    }

    [Fact]
    public void Layout_ShouldPointSearchFormToSearchController()
    {
        var layout = LoadLayoutSource();

        layout.Should().MatchRegex(
            "asp-controller=\"Search\"\\s+asp-action=\"Index\"",
            "search form musí mířit na SearchController.Index");
    }

    [Fact]
    public void Layout_ShouldUseGovDesignSystemAttributes()
    {
        var layout = LoadLayoutSource();

        // size a color jsou oficiální atributy gov-form-search Web Component
        layout.Should().Contain("size=\"m\"", "gov-form-search musí mít atribut size");
        layout.Should().Contain("color=\"primary\"", "gov-form-search musí mít atribut color");
    }
}
