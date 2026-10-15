using System.Text.RegularExpressions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Minimální čtečka aplikačního CSS pro kontrolní testy barev (2026-10-15). Rozumí pravidlům
/// i vnořeným @media (vnitřní pravidlo se najde samo, prelude @media se přeskočí).
/// </summary>
internal static class AppCss
{
    public sealed record Declaration(string File, int Line, string Selector, string Property, string Value);

    public static string RepoRoot { get; } = LocateRepositoryRoot();

    public static string CssDir => Path.Combine(RepoRoot, "PmTracker.Web", "wwwroot", "css");

    public static string GovStylesDir => Path.Combine(RepoRoot, "PmTracker.Web", "wwwroot", "assets", "gov", "styles");

    /// <summary>Aplikační CSS bez tisku (pdf-export.css je mimo rozsah — barvy tisku dává spec 2026-09-05).</summary>
    public static IReadOnlyList<string> AppCssFiles() =>
        Directory.GetFiles(CssDir, "*.css", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(CssDir, path).Replace('\\', '/'))
            .Where(path => path != "pdf-export.css")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    public static string StripComments(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", m => new string('\n', m.Value.Count(c => c == '\n')), RegexOptions.Singleline);

    public static IEnumerable<Declaration> Declarations(string relPath)
    {
        var css = StripComments(File.ReadAllText(Path.Combine(CssDir, relPath)));
        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < css.Length; i++)
        {
            if (css[i] == '\n') lineStarts.Add(i + 1);
        }

        int LineOf(int offset)
        {
            var index = lineStarts.BinarySearch(offset);
            return (index >= 0 ? index : ~index - 1) + 1;
        }

        foreach (Match rule in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}"))
        {
            var selector = Regex.Replace(rule.Groups[1].Value, @"\s+", " ").Trim();
            var body = rule.Groups[2];
            foreach (Match declaration in Regex.Matches(body.Value, @"(--[\w-]+|[a-zA-Z-]+)\s*:\s*([^;]+)"))
            {
                yield return new Declaration(
                    relPath,
                    LineOf(body.Index + declaration.Index),
                    selector,
                    declaration.Groups[1].Value.Trim().ToLowerInvariant(),
                    declaration.Groups[2].Value.Trim());
            }
        }
    }

    public static bool IsColorProperty(string property) =>
        property.StartsWith("--", StringComparison.Ordinal)
        || Regex.IsMatch(property,
            @"^(color|background(-color|-image)?|border(-(top|right|bottom|left|block|inline)(-(start|end))?)?(-color)?|outline(-color)?|box-shadow|text-shadow|fill|stroke|caret-color|accent-color|text-decoration(-color)?|column-rule(-color)?)$");

    /// <summary>Všechny custom properties definované v gov stylech a v aplikačním CSS.</summary>
    public static HashSet<string> DefinedVariables()
    {
        var defined = new HashSet<string>(StringComparer.Ordinal);
        var sources = Directory.GetFiles(GovStylesDir, "*.css")
            .Concat(AppCssFiles().Select(file => Path.Combine(CssDir, file)));
        foreach (var path in sources)
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(path), @"(--[\w-]+)\s*:"))
            {
                defined.Add(match.Groups[1].Value);
            }
        }

        return defined;
    }

    private static string LocateRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
    }
}
