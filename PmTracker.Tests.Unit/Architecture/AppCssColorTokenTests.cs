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
}
