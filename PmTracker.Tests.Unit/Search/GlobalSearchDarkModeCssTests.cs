using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Ověřuje dark mode CSS pro globální vyhledávání.
/// POZOR: gov-form-search, gov-form-input a gov-button jsou nyní skutečné Web Components
/// z @gov-design-system-ce/components (CDN) — jejich dark mode řídí design system.
/// Vlastní dark overrides pro gov-* třídy v site.css jsou ZAKÁZÁNY.
/// Testujeme pouze naše vlastní komponenty: dropdown a badge.
/// </summary>
public sealed class GlobalSearchDarkModeCssTests
{
    private static string LoadSiteCss()
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

        var cssPath = Path.Combine(
            directory.FullName,
            "PmTracker.Web",
            "wwwroot",
            "css",
            "site.css");

        File.Exists(cssPath).Should().BeTrue($"site.css musí existovat na cestě {cssPath}");
        return File.ReadAllText(cssPath);
    }

    [Fact]
    public void Css_NemaVlastniDarkOverride_Pro_GovFormSearch()
    {
        var css = LoadSiteCss();
        // gov-form-search dark mode řídí design system — naše overrides by konfliktovaly
        css.Should().NotContain("[data-theme=\"dark\"] .gov-form-search",
            "site.css nesmí mít vlastní dark override pro .gov-form-search — to řídí Web Component");
    }

    [Fact]
    public void Css_NemaVlastniDarkOverride_Pro_GovFormInputInput()
    {
        var css = LoadSiteCss();
        css.Should().NotContain("[data-theme=\"dark\"] .gov-form-input__input",
            "site.css nesmí mít vlastní dark override pro .gov-form-input__input — to řídí Web Component");
    }

    [Fact]
    public void Css_MaDarkMode_Override_Pro_AppSearchDropdown()
    {
        var css = LoadSiteCss();
        css.Should().Contain("[data-theme=\"dark\"] .app-search-dropdown",
            "site.css musí mít dark mode override pro .app-search-dropdown (naše vlastní komponenta)");
    }

    [Fact]
    public void Css_MaSuggestDlazdicoveStyley()
    {
        var css = LoadSiteCss();
        css.Should().Contain(".global-search-item", "CSS musí obsahovat styl pro suggest dlaždice (.global-search-item)");
        css.Should().Contain(".global-search-item__title", "CSS musí obsahovat styl pro nadpis dlaždice");
        css.Should().Contain(".global-search-item__badge", "CSS musí obsahovat styl pro badge typu");
        css.Should().Contain(".global-search-item__snippet", "CSS musí obsahovat styl pro snippet");
    }

    [Fact]
    public void Css_DarkMode_MaOverride_ProBadgeBarvy()
    {
        var css = LoadSiteCss();
        css.Should().Contain("[data-theme=\"dark\"] .global-search-item__badge--zaznam",
            "dark mode musí mít override pro barvu badge záznamu");
        css.Should().Contain("[data-theme=\"dark\"] .global-search-item__badge--vyjadreni",
            "dark mode musí mít override pro barvu badge vyjádření");
    }
}
