using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// A2 (2026-07-08): dlaždice dashboardu — titulek --d-fs-base (ne --d-fs-title),
/// meta --d-fs-label; těsnější svislé odsazení. Cíl: dlaždice jednání ≤120px na 13".
/// </summary>
public sealed class DashboardTileTypographyTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Css => File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));

    private static string Block(string selectorRegex)
    {
        var m = Regex.Match(Css, selectorRegex + @"[^{]*\{[^}]*\}", RegexOptions.Singleline);
        m.Success.Should().BeTrue(selectorRegex);
        return m.Value;
    }

    [Fact]
    public void TileTitles_UseBaseFont()
    {
        Block(@"\.dashboard-meeting-title").Should().Contain("var(--d-fs-base");
        Block(@"\.dashboard-focus-title").Should().Contain("var(--d-fs-base");
        Block(@"\.dashboard-meeting-title").Should().NotContain("--d-fs-title");
    }

    [Fact]
    public void TileMeta_UsesLabelFont()
    {
        Block(@"\.dashboard-item-meta").Should().Contain("var(--d-fs-label");
    }

    [Fact]
    public void PanelLists_UseScrollSnapAndBottomFade()
    {
        var list = Block(@"\.dashboard-focus-list");
        list.Should().Contain("scroll-snap-type", "B1: snap drží celé dlaždice po doscrollování");
        list.Should().Contain("mask-image", "B1: fade signalizuje pokračování obsahu");
        Block(@"\.dashboard-focus-item").Should().Contain("scroll-snap-align");
    }

    /// <summary>C2 rev. 2 (2026-07-10): jednání nikdy nestlačit pod obsah (shrink 0),
    /// strop 50 % sloupce; novinky berou zbytek. Jen letité flex základy — bez :has
    /// a intrinsic keywords v nosné logice (Edge na i15 počítal max-content stlačeně).</summary>
    [Fact]
    public void RailPanels_MeetingsContentSizedWithHalfCap_NewsTakeRest()
    {
        var meetings = Block(@"\.dashboard-rail > \.dashboard-section--meetings");
        meetings.Should().Contain("flex: 0 0 auto", "jednání = přesně obsah, shrink 0 = nic je nestlačí");
        meetings.Should().NotContain("max-content", "intrinsic keyword byl zdroj i15 regrese");

        // Strop 50 % jen když novinky mají obsah — jedno-úrovňové :has (vnořené je neplatné).
        Block(@"\.dashboard-rail > \.dashboard-section--meetings:has\(\+ \.dashboard-section--news \.dashboard-news-list\)")
            .Should().Contain("max-height: 50%");
        Css.Should().NotMatchRegex(@":has\([^)]*:has\(", ":has nelze vnořovat — neplatný selektor se celý zahodí");

        Block(@"\.dashboard-rail > \.dashboard-section--news")
            .Should().Contain("flex: 1 1 auto", "novinky si vezmou zbytek");

        // :has smí zůstat jen jako enhancement (prázdné novinky → jednání přes polovinu),
        // nosné dělení na něm stát nesmí.
        Regex.IsMatch(Css, @"\.dashboard-rail > \.dashboard-panel:has\([^)]*\)\s*[,{]")
            .Should().BeFalse("nosná rail pravidla nesmí být gated přes :has");
    }

    /// <summary>C2 (2026-07-10): dlaždice jednání = flex-wrap tok (1 řádka na širokém panelu).</summary>
    [Fact]
    public void MeetingTile_FlowsAsFlexWrap()
    {
        var m = Regex.Match(Css,
            @"/\* C2 [^*]*dlaždice jednání[^*]*\*/\s*\.dashboard-meeting-item\s*\{[^}]*\}",
            RegexOptions.Singleline);
        m.Success.Should().BeTrue("C2 override blok dlaždice jednání existuje (za sdíleným border/padding blokem)");
        m.Value.Should().Contain("display: flex");
        m.Value.Should().Contain("flex-wrap: wrap");
    }

    /// <summary>C2 (2026-07-10): jen dlaždice jednání — focus/news layout beze změny.</summary>
    [Fact]
    public void FocusAndNewsTiles_KeepExistingLayout()
    {
        // První výskyt .dashboard-focus-item je sdílený border blok — grid je v samostatném bloku.
        Regex.IsMatch(Css, @"\.dashboard-focus-item\s*\{[^}]*display: grid", RegexOptions.Singleline)
            .Should().BeTrue("focus dlaždice zůstává 3-col grid");
        Regex.IsMatch(Css, @"\.dashboard-news-item\s*\{[^}]*flex-wrap", RegexOptions.Singleline)
            .Should().BeFalse("news dlaždice nemá C2 flex-wrap přebírat");
    }
}
