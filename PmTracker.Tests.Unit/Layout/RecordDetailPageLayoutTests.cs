using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Stránka záznamu (2026-07-14): hlavička na plnou šířku + dva sloupce 40/60,
/// pod 1100 px stohování. Jen letité CSS konstrukce (i15 pojistka).
/// </summary>
public sealed class RecordDetailPageLayoutTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Css => Read("PmTracker.Web/wwwroot/css/site.css");

    /// <summary>
    /// Markup view BEZ Razor komentářů — piny hlídají skutečný markup, ne vysvětlující
    /// prózu. (Komentáře smějí zmiňovat i to, co se v markupu záměrně nepoužívá.)
    /// </summary>
    private static string View => Regex.Replace(
        Read("PmTracker.Web/Views/Projekty/ZaznamDetailPage.cshtml"),
        @"@\*[\s\S]*?\*@",
        string.Empty);

    /// <summary>Úsek CSS patřící stránce záznamu.</summary>
    private static string PageSlice()
    {
        var css = Css;
        var start = css.IndexOf(".record-page-grid", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "sekce stránky záznamu musí v site.css existovat");
        var end = css.IndexOf("/* === konec stránky záznamu === */", start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, "sekce musí být ohraničená koncovou značkou");
        return css[start..end];
    }

    [Fact]
    public void Grid_HasTwoColumns_WithStackingBelow1100()
    {
        var slice = PageSlice();
        slice.Should().Contain("grid-template-columns: 40% 1fr",
            "vlevo vyjádření ~40 %, vpravo harmonogram zbytek (spec U1)");
        Regex.IsMatch(Css, @"@media \(max-width: 1100px\)\s*\{[^@]*\.record-page-grid", RegexOptions.Singleline)
            .Should().BeTrue("pod 1100 px se sloupce skládají pod sebe");
    }

    [Fact]
    public void PageBlock_AvoidsFragileModernCss()
    {
        var slice = PageSlice();
        slice.Should().NotContain(":has(", "i15 pojistka — nosná logika bez :has");
        slice.Should().NotContain("max-content", "i15 pojistka — bez intrinsic keywords");
        slice.Should().NotContain("min-content", "i15 pojistka — bez intrinsic keywords");
        slice.Should().NotContain("fit-content", "i15 pojistka — bez intrinsic keywords");
    }

    [Fact]
    public void ScheduleViewMode_IsDrivenByContainerClass()
    {
        var slice = PageSlice();
        slice.Should().Contain(".record-page-schedule--table [data-record-page-schedule-graph]",
            "v tabulkovém režimu se skryje graf");
        slice.Should().Contain(".record-page-schedule:not(.record-page-schedule--table) [data-record-page-schedule-table]",
            "ve výchozím (grafickém) režimu se skryje tabulka");
    }

    [Fact]
    public void CommentsWrapper_IsResetCard_WithoutCollapse()
    {
        var slice = PageSlice();
        slice.Should().Contain(".record-card--page-column",
            "wrapper karty kolem vyjádření má resetovaný vzhled");
        View.Should().NotContain("record-card--page-column collapsed",
            "wrapper nesmí být collapsed — skryl by tělo s vyjádřeními");
        View.Should().NotContain("data-record-toggle",
            "na stránce se nic nesbaluje, wrapper nemá toggle hlavičku");
    }

    [Fact]
    public void Page_UsesWideFluidTier_LikeProjectDetail()
    {
        // 2026-09-03: stránka byla ve vycentrovaném sloupci 1280 px s prázdnými kraji.
        // Široký (fluid) tier se aktivuje BodyClass "dashboard-page" — stejně jako
        // detail projektu a detail jednání.
        View.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "stránka má využít celou šířku, ne vycentrovaný sloupec");
    }

    [Fact]
    public void WithoutSchedule_CommentsSpanFullWidth()
    {
        // 2026-09-03: bez harmonogramu zabíralo vyjádření jen levých 40 % a zbytek
        // zůstal prázdný. Modifikátor přidává server (žádné :has — i15 pojistka).
        View.Should().Contain("record-page-grid--single",
            "bez harmonogramu dostane grid jednosloupcový modifikátor");
        PageSlice().Should().Contain(".record-page-grid--single",
            "modifikátor má v CSS pravidlo na jeden sloupec");
        Regex.IsMatch(PageSlice(), @"\.record-page-grid--single\s*\{[^}]*grid-template-columns:\s*1fr")
            .Should().BeTrue("jednosloupcový režim roztáhne vyjádření na celou šířku");
    }

    [Fact]
    public void Toggle_IsButtonPair_NotGovSwitch()
    {
        View.Should().Contain("data-schedule-view-toggle=\"graph\"");
        View.Should().Contain("data-schedule-view-toggle=\"table\"");
        View.Should().Contain("aria-pressed", "segmentovaný přepínač hlásí stav přes aria-pressed");
        View.Should().NotContain("gov-form-switch",
            "přepínáme mezi dvěma pojmenovanými pohledy, ne zapnuto/vypnuto (spec §6)");
    }
}
