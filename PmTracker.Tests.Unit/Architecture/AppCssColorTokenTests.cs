using System.Text.RegularExpressions;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Barvy aplikace = sémantické tokeny gov DS 4.7.0 (2026-10-15, memory feedback_dark_mode_gov_tokens).
/// Tmavý motiv přepíná gov sám; aplikace nesmí držet vlastní paletu ani tmavá pravidla na komponentách.
/// </summary>
public sealed class AppCssColorTokenTests
{
    /// <summary>Proměnné nastavované až za běhu (inline style ve view / JS) — nejsou v žádném CSS.</summary>
    private static readonly HashSet<string> RuntimeVariables = new(StringComparer.Ordinal)
    {
        "--seg-color",                     // _ScheduleBlock.cshtml: barva kroku z Excelu
        "--record-schedule-delay-color",   // _ScheduleBlock.cshtml: barva skluzu
    };

    private static readonly Regex LegacyColorVariable = new(
        @"--(gov-color-[\w-]+|app-text-primary|app-muted|app-nav-[\w-]+|app-bg-[\w-]+|pm-surface[\w-]*|pm-text[\w-]*|pm-border|pm-warning[\w-]*|pm-color-[\w-]+|record-cat-decision|schedule-step-\d+|schedule-delay|gov-shadow-soft)\b");

    [Fact]
    public void LegacyColorVariables_AreNeitherDefinedNorUsed()
    {
        var offenders = AppCss.AppCssFiles()
            .SelectMany(file => AppCss.StripComments(File.ReadAllText(Path.Combine(AppCss.CssDir, file)))
                .Split('\n')
                .Select((text, index) => (file, line: index + 1, text))
                .Where(x => LegacyColorVariable.IsMatch(x.text))
                .Select(x => $"{x.file}:{x.line}: {x.text.Trim()}"))
            .ToList();

        offenders.Should().BeEmpty("stará paleta aplikace (--gov-color-*, --app-*, --pm-*) je nahrazená gov tokeny");
    }

    [Fact]
    public void ColorProperties_ReferenceOnlyDefinedVariables()
    {
        var defined = AppCss.DefinedVariables();
        var offenders = AppCss.AppCssFiles()
            .SelectMany(AppCss.Declarations)
            .Where(d => AppCss.IsColorProperty(d.Property))
            .SelectMany(d => Regex.Matches(d.Value, @"var\((--[\w-]+)").Select(m => (d, name: m.Groups[1].Value)))
            .Where(x => !defined.Contains(x.name) && !RuntimeVariables.Contains(x.name))
            .Select(x => $"{x.d.File}:{x.d.Line} {x.d.Selector} {{ {x.d.Property}: {x.d.Value} }} → {x.name}")
            .ToList();

        offenders.Should().BeEmpty("nedefinovaná proměnná spadne na záložní (světlou) hodnotu i v tmavém motivu");
    }

    private static readonly Regex ColorLiteral = new(@"#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?)\(|(?<![\w-])(white|black)(?![\w-])", RegexOptions.IgnoreCase);

    /// <summary>Pojmenované výjimky: hlavička je podle zadání 2026-10-15 zmražená.</summary>
    private static readonly HashSet<string> AllowedLiterals = new(StringComparer.Ordinal)
    {
        ".app-logo|background",   // rámeček loga v hlavičce zůstává bílý v obou motivech
    };

    public static IEnumerable<object[]> FilesWithoutColorLiterals() =>
        AppCss.AppCssFiles().Where(file => file != "tokens.css").Select(file => new object[] { file });

    [Theory]
    [MemberData(nameof(FilesWithoutColorLiterals))]
    public void ColorProperties_UseTokensNotLiterals(string file)
    {
        var offenders = AppCss.Declarations(file)
            .Where(d => AppCss.IsColorProperty(d.Property))
            .Where(d => ColorLiteral.IsMatch(d.Value))
            .Where(d => !AllowedLiterals.Contains($"{d.Selector}|{d.Property}"))
            .Select(d => $"{d.File}:{d.Line} {d.Selector} {{ {d.Property}: {d.Value} }}")
            .ToList();

        offenders.Should().BeEmpty("barva patří do gov tokenu (nebo aplikačního --app-* v tokens.css), jinak se v tmavém motivu nepřepne");
    }

    [Fact]
    public void DarkThemeSelectors_OnlyInTokensCss()
    {
        var offenders = AppCss.AppCssFiles()
            .Where(file => file != "tokens.css")
            .SelectMany(AppCss.Declarations)
            .Where(d => d.Selector.Contains("data-theme", StringComparison.Ordinal))
            .Select(d => $"{d.File}:{d.Line} {d.Selector}")
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty("tmavý motiv přepínají gov tokeny; pravidlo :root[data-theme=\"dark\"] .x je ruční paleta navíc");
    }

    [Fact]
    public void TokensCss_DarkBlockOnlyDefinesTokensAndColorScheme()
    {
        var offenders = AppCss.Declarations("tokens.css")
            .Where(d => d.Selector.Contains("data-theme", StringComparison.Ordinal))
            .Where(d => !d.Property.StartsWith("--", StringComparison.Ordinal) && d.Property != "color-scheme")
            .Select(d => $"tokens.css:{d.Line} {d.Selector} {{ {d.Property} }}")
            .ToList();

        offenders.Should().BeEmpty();
    }

    /// <summary>
    /// Vykrojené rohy záložek pm-tab (kruhy ::before/::after) se kreslí barvou plochy, na které
    /// záložky leží — panel/karta, ne stránka. S --background-page kreslily v tmavém motivu černý kruh.
    /// </summary>
    [Fact]
    public void PmTabCornerCircles_DoNotUsePageBackground()
    {
        var offenders = AppCss.Declarations("site.css")
            .Where(d => d.Selector.Contains("data-pm-tab-inner]::", StringComparison.Ordinal))
            .Where(d => d.Value.Contains("--background-page", StringComparison.Ordinal))
            .Select(d => $"site.css:{d.Line} {d.Selector}")
            .ToList();

        offenders.Should().BeEmpty("roh záložky musí mít barvu panelu (--background-block-*)");
    }

    /// <summary>
    /// Hover na ploše bloku: v tmavém motivu je --background-neutral-subtlest = --background-block-primary
    /// (38,38,38), hover by splynul. Plocha pod kurzorem patří --background-block-secondary (gov: 246 / 69).
    /// </summary>
    [Fact]
    public void HoverStates_DoNotUseColorsEqualToBlockSurfaceInDarkTheme()
    {
        var offenders = AppCss.AppCssFiles()
            .SelectMany(AppCss.Declarations)
            .Where(d => Regex.IsMatch(d.Selector, @":hover|:focus|is-active|aria-selected"))
            .Where(d => d.Property.StartsWith("background", StringComparison.Ordinal))
            .Where(d => Regex.IsMatch(d.Value, @"var\(--background-(neutral-subtlest|block-primary)\)"))
            .Select(d => $"{d.File}:{d.Line} {d.Selector}")
            .ToList();

        offenders.Should().BeEmpty("hover musí být v obou motivech vidět");
    }

    /// <summary>
    /// var(--x) bez záložní hodnoty u proměnné, kterou žádné CSS nedefinuje, zneplatní celou
    /// deklaraci (např. border-radius spadne na 0). Nedefinované proměnné (nastavované až z JS
    /// nebo inline) si proto záložní hodnotu musí nechat.
    /// </summary>
    [Fact]
    public void VariablesWithoutFallback_AreDefined()
    {
        var defined = AppCss.DefinedVariables();
        var offenders = AppCss.AppCssFiles()
            .SelectMany(AppCss.Declarations)
            .SelectMany(d => Regex.Matches(d.Value, @"var\((--[\w-]+)\)").Select(m => (d, name: m.Groups[1].Value)))
            .Where(x => !defined.Contains(x.name) && !RuntimeVariables.Contains(x.name))
            .Select(x => $"{x.d.File}:{x.d.Line} {x.d.Selector} {{ {x.d.Property}: {x.d.Value} }}")
            .ToList();

        offenders.Should().BeEmpty($"nedefinovaná proměnná bez záložní hodnoty zneplatní deklaraci:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }
}
