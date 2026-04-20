using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Zabraňuje regresi: každý <gov-icon name="X" type="Y"> musí odkazovat na
/// soubor, který skutečně existuje v /wwwroot/assets/icons/{type}/{name}.svg.
///
/// Historie: 2026-04-20 ranní fix zavedl icon-only tlačítka s type="basic",
/// ale složka /assets/icons/basic/ neexistuje (ikony jsou v /components/).
/// Výsledek: prázdná tlačítka v UI, 404 v console. Tento test to zachytí
/// při buildu před release.
/// </summary>
public sealed class GovIconReferencesTests
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

    private static readonly Regex GovIconRegex = new(
        @"<gov-icon\b[^>]*\bname=""(?<name>[^""]+)""[^>]*\btype=""(?<type>[^""]+)""|<gov-icon\b[^>]*\btype=""(?<type2>[^""]+)""[^>]*\bname=""(?<name2>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void GovIcon_References_InViews_MustResolveToExistingAssets()
    {
        var repoRoot = RepoRoot().FullName;
        var viewsDir = Path.Combine(repoRoot, "PmTracker.Web", "Views");
        var iconsDir = Path.Combine(repoRoot, "PmTracker.Web", "wwwroot", "assets", "icons");

        Directory.Exists(viewsDir).Should().BeTrue();
        Directory.Exists(iconsDir).Should().BeTrue();

        var viewFiles = Directory.EnumerateFiles(viewsDir, "*.cshtml", SearchOption.AllDirectories);
        var missing = new List<string>();

        foreach (var file in viewFiles)
        {
            var content = File.ReadAllText(file);
            foreach (Match m in GovIconRegex.Matches(content))
            {
                var iconName = m.Groups["name"].Success ? m.Groups["name"].Value : m.Groups["name2"].Value;
                var iconType = m.Groups["type"].Success ? m.Groups["type"].Value : m.Groups["type2"].Value;
                if (string.IsNullOrWhiteSpace(iconName) || string.IsNullOrWhiteSpace(iconType))
                    continue;

                var expectedPath = Path.Combine(iconsDir, iconType, iconName + ".svg");
                if (!File.Exists(expectedPath))
                {
                    var relativeView = Path.GetRelativePath(repoRoot, file);
                    missing.Add($"{relativeView}: <gov-icon name=\"{iconName}\" type=\"{iconType}\"> → očekávaný soubor {Path.GetRelativePath(repoRoot, expectedPath)} neexistuje");
                }
            }
        }

        missing.Should().BeEmpty(
            "každá reference <gov-icon name=X type=Y> musí odkazovat na existující soubor v wwwroot/assets/icons/{type}/{name}.svg");
    }
}
