using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// 2026-06-29: zvýraznění změněného pole v návrhu (oranžová .proposal-field-changed) podbarvovalo
/// jen ikonu — chybělo mu padding, takže se pozadí schovalo za vlastní pozadí date-fieldu. Zelená
/// .proposal-field-editable má padding (rámeček kolem celého pole) → podbarví celé pole. Oranžová
/// musí mít stejné chování (padding + srovnatelná sytost), aby se podbarvilo celé pole, ne jen ikona.
/// </summary>
public sealed class ProposalFieldHighlightCssTests
{
    private static string Css()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return File.ReadAllText(
            Path.Combine(dir!.FullName, "PmTracker.Web", "wwwroot", "css", "site.css"));
    }

    /// <summary>Tělo základního pravidla selektoru (ne vnořeného `selektor .child`).</summary>
    private static string BaseRuleBody(string css, string selector)
    {
        var m = Regex.Match(css, Regex.Escape(selector) + @"\s*\{([^}]*)\}");
        m.Success.Should().BeTrue($"pravidlo {selector} {{ … }} musí existovat v site.css");
        return m.Groups[1].Value;
    }

    [Fact]
    public void ProposalFieldChanged_HasPadding_LikeEditable()
    {
        var css = Css();

        BaseRuleBody(css, ".proposal-field-changed").Should().Contain("padding",
            ".proposal-field-changed musí mít padding (rámeček kolem celého pole), jinak se podbarví jen ikona");
        // Parita se zelenou editable variantou — ať je oranžová stejně viditelná „celá".
        BaseRuleBody(css, ".proposal-field-editable").Should().Contain("padding");
    }

    [Fact]
    public void ScheduleStackedInput_Changed_FillsWholeCell()
    {
        var css = Css();

        // .schedule-stacked-input má vlastní bílé pozadí deklarované POZDĚJI než .proposal-field-changed,
        // takže obecné pravidlo ho nepřebije (stejná specificita → vyhrává pozdější). Potřebujeme
        // specifické pravidlo .schedule-stacked-input.proposal-field-changed (override) + průhledné
        // vnitřní pozadí, aby byla oranžová CELÁ buňka, ne jen ikona/rámeček.
        css.Should().MatchRegex(
            @"\.schedule-stacked-input\.proposal-field-changed\s*\{[^}]*background",
            "celá buňka schedule-stacked-input musí být při změně podbarvená (override bílého pozadí buňky)");
        css.Should().MatchRegex(
            @"\.schedule-stacked-input\.proposal-field-changed[^{]*\.app-date-display-input[\s\S]{0,400}background:\s*transparent",
            "vnitřní input musí být průhledný, aby prosvítalo oranžové pozadí celé buňky");
    }
}
