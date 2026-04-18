using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Ověřuje, že theme switch v _Layout.cshtml je implementován jako skutečná
/// gov-theme-switch Web Component (ne CSS aproximace) a že CSS site.css
/// neobsahuje kolidující pravidla pro odstraněnou aproximaci.
/// </summary>
public sealed class ThemeSwitchGovComponentTests
{
    private static DirectoryInfo RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("repozitář s PmTracker.sln musí být dostupný");
        return directory!;
    }

    private static string ReadLayout()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Views", "Shared", "_Layout.cshtml");
        File.Exists(path).Should().BeTrue($"soubor {path} musí existovat");
        return File.ReadAllText(path);
    }

    private static string ReadSiteCss()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "site.css");
        File.Exists(path).Should().BeTrue($"soubor {path} musí existovat");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Layout musí obsahovat gov-theme-switch nebo gov-form-switch Web Component tag.
    /// </summary>
    [Fact]
    public void Layout_ObsahujeGovThemeSwitchNeboGovFormSwitchTag()
    {
        var content = ReadLayout();

        var hasGovThemeSwitch = content.Contains("<gov-theme-switch", StringComparison.Ordinal);
        var hasGovFormSwitch = content.Contains("<gov-form-switch", StringComparison.Ordinal);

        (hasGovThemeSwitch || hasGovFormSwitch).Should().BeTrue(
            "_Layout.cshtml musí obsahovat <gov-theme-switch> nebo <gov-form-switch> Web Component tag");
    }

    /// <summary>
    /// Layout nesmí obsahovat původní CSS aproximaci — span s ikonami slunce a měsíce.
    /// </summary>
    [Fact]
    public void Layout_NeobsahujeSpanGovThemeSwitchIconSun()
    {
        var content = ReadLayout();

        content.Should().NotContain("gov-theme-switch-icon-sun",
            "_Layout.cshtml nesmí obsahovat starou CSS aproximaci gov-theme-switch-icon-sun (nahrazena Web Componentou)");
    }

    /// <summary>
    /// Layout musí stále obsahovat data-theme-switch root element pro JS a testy.
    /// </summary>
    [Fact]
    public void Layout_ObsahujeDataThemeSwitchRoot()
    {
        var content = ReadLayout();

        content.Should().Contain("data-theme-switch",
            "_Layout.cshtml musí obsahovat data-theme-switch atribut (root pro theme.js a testy)");
    }

    /// <summary>
    /// CSS site.css nesmí obsahovat kolidující .gov-theme-switch-icon-sun / -moon pravidla
    /// (odstraněná CSS aproximace). Samostatné .gov-switch pravidla mohou zůstat pro filtry.
    /// </summary>
    [Fact]
    public void SiteCss_NeobsahujeKolidujiciGovThemeSwitchIconPravidla()
    {
        var content = ReadSiteCss();

        content.Should().NotContain(".gov-theme-switch-icon-sun",
            "site.css nesmí obsahovat .gov-theme-switch-icon-sun (CSS aproximace odstraněna)");
        content.Should().NotContain(".gov-theme-switch-icon-moon",
            "site.css nesmí obsahovat .gov-theme-switch-icon-moon (CSS aproximace odstraněna)");
        content.Should().NotContain(".gov-theme-switch-icon {",
            "site.css nesmí obsahovat .gov-theme-switch-icon pravidlo (CSS aproximace odstraněna)");
        content.Should().NotContain(".gov-theme-switch-thumb",
            "site.css nesmí obsahovat .gov-theme-switch-thumb pravidlo (CSS aproximace odstraněna)");
        content.Should().NotContain(".gov-theme-switch-track",
            "site.css nesmí obsahovat .gov-theme-switch-track pravidlo (CSS aproximace odstraněna)");
    }

    /// <summary>
    /// CSS site.css nesmí obsahovat selektor .gov-theme-switch jako samostatný blok
    /// (byl součástí CSS aproximace). Neměl by se zaměňovat s gov-theme-switch Web Component.
    /// </summary>
    [Fact]
    public void SiteCss_NeobsahujeGovThemeSwitchTriduSelector()
    {
        var content = ReadSiteCss();

        // Hledáme .gov-theme-switch jako CSS třídu (s tečkou), ne jako tag nebo text v komentáři
        var hasClassSelector = Regex.IsMatch(content, @"\.gov-theme-switch[\s,{]");

        hasClassSelector.Should().BeFalse(
            "site.css nesmí obsahovat .gov-theme-switch jako CSS třídu (CSS aproximace byla odstraněna; gov-theme-switch je nyní Web Component se shadow DOM)");
    }

    /// <summary>
    /// Layout stále obsahuje server-side rendering thématu přes cookie pmtracker.theme.mode.
    /// </summary>
    [Fact]
    public void Layout_ObsahujeServerSideCookieTheme()
    {
        var content = ReadLayout();

        content.Should().Contain("pmtracker.theme.mode",
            "_Layout.cshtml musí číst cookie pmtracker.theme.mode pro SSR tématu (first paint bez flash)");
        content.Should().Contain("data-theme=",
            "_Layout.cshtml musí nastavovat data-theme atribut na <html> z cookie");
    }
}
