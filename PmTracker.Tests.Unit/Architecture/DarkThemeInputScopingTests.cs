using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Guard: globální dark-theme override pro nativní input[type=...] MUSÍ být
/// scopovaný tak, aby se neaplikoval uvnitř gov-form-input / gov-form-search
/// Web Components (jejichž slot input je v light DOM).
///
/// Bug 2026-04-19 večer: řádek site.css:5243 `:root[data-theme="dark"] input[type="text"]`
/// bez exclusionu → gov-form-search vnitřní input dostal bg #111b2d nepříbuzný s
/// host wrapperem → user: "ve tmavém režimu je uvnitř search pole tmavý element".
/// </summary>
public sealed class DarkThemeInputScopingTests
{
    private static string LocateRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
        }

        return directory.FullName;
    }

    [Theory]
    [InlineData("text")]
    [InlineData("email")]
    [InlineData("date")]
    [InlineData("time")]
    [InlineData("datetime-local")]
    [InlineData("number")]
    [InlineData("search")]
    public void DarkThemeInputOverride_MustExcludeGovWebComponents(string inputType)
    {
        var cssPath = Path.Combine(LocateRepositoryRoot(), "PmTracker.Web", "wwwroot", "css", "site.css");
        var source = File.ReadAllText(cssPath);

        // Selektor musí mít každou z těchto exclusion — jinak globální pravidlo
        // pronikne do gov-form-input vnitřního <input type="text"> a pokazí vizuál.
        var pattern = new Regex(
            $@":root\[data-theme=""dark""\]\s+input\[type=""{Regex.Escape(inputType)}""\]:not\(gov-form-input input\):not\(gov-form-search input\):not\(\[role=""search""\] input\)");

        pattern.IsMatch(source).Should().BeTrue(
            $"dark-theme override pro input[type=\"{inputType}\"] musí mít scope :not(gov-form-input input):not(gov-form-search input):not([role=\"search\"] input)");
    }
}
