using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Regression guard (2026-04-21, úprava #13):
/// `pm-button` tag renderuje jako `gov-button`. Web component při hydraci
/// přesune `name` atribut na vnitřní `<button class="element">`, ale
/// `value` atribut se NEPROPAGUJE. Form submit pošle name s prázdnou hodnotou
/// → model binding použije default property value → oba submit buttony
/// posílají stejnou hodnotu.
///
/// Vzor vedoucí k bugu:
/// <pm-button native-type="submit" name="X" value="a">A</pm-button>
/// <pm-button native-type="submit" name="X" value="b">B</pm-button>
/// (obě posílají X="" → default X="a" → B jde jako A)
///
/// Správný pattern: Direction v hidden input, pm-button jen submit.
/// <input type="hidden" name="X" value="a" />
/// <pm-button native-type="submit">A</pm-button>
/// </summary>
public sealed class PmButtonSubmitPatternTests
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

    private static readonly Regex PmButtonSubmitWithValue = new(
        @"<pm-button\b[^>]*\bnative-type=""submit""[^>]*\bvalue=""[^""]*""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void PmButton_SubmitVariant_MustNotUseValueAttribute()
    {
        var repoRoot = RepoRoot().FullName;
        var viewsDir = Path.Combine(repoRoot, "PmTracker.Web", "Views");

        Directory.Exists(viewsDir).Should().BeTrue();

        var viewFiles = Directory.EnumerateFiles(viewsDir, "*.cshtml", SearchOption.AllDirectories);
        var offenders = new List<string>();

        foreach (var file in viewFiles)
        {
            var content = File.ReadAllText(file);
            foreach (Match m in PmButtonSubmitWithValue.Matches(content))
            {
                var relative = Path.GetRelativePath(repoRoot, file);
                var snippet = m.Value.Length > 120 ? m.Value.Substring(0, 120) + "…" : m.Value;
                offenders.Add($"{relative}: {snippet}");
            }
        }

        offenders.Should().BeEmpty(
            "gov-button hydrace nepropaguje `value` na vnitřní button → form submit pošle Direction=\"\" → " +
            "model binding použije default. Použij místo toho <input type=\"hidden\" name=\"X\" value=\"...\" />");
    }
}
