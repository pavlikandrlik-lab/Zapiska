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

        // Struktura přesně podle @gov-design-system-ce/styles (gov-form-search)
        layout.Should().Contain("class=\"gov-form-search\"", "wrapper musí mít gov-form-search třídu (viz designsystem.gov.cz)");
        layout.Should().Contain("slot=\"input\"", "vstupní pole je umístěno ve slotu input");
        layout.Should().Contain("slot=\"button\"", "tlačítko je umístěno ve slotu button");

        layout.Should().Contain("class=\"gov-form-input__input\"", "input musí mít BEM třídu gov-form-input__input");
        layout.Should().Contain("type=\"search\"", "input musí mít search type");
        layout.Should().Contain("name=\"q\"", "input musí mít jméno q");

        layout.Should().Contain("class=\"gov-button\"", "submit musí mít gov-button třídu");
        layout.Should().Contain("type=\"submit\"", "submit tlačítko musí být klikatelné");
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

        // data-size a data-color jsou oficiální atributy gov-form-search
        layout.Should().Contain("data-size=\"m\"", "gov-form-search musí mít atribut data-size");
        layout.Should().Contain("data-color=\"neutral\"", "gov-form-search musí mít atribut data-color");
    }
}
