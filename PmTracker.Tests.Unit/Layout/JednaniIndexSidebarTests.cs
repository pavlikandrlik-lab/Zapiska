using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Jednani/Index sidebar layout (2026-04-20): levý projekt-sidebar +
/// pravý meetings area. BodyClass = dashboard-page (fluid tier) aby
/// layout měl dostatek šířky. Projekt-karta má 2-sloupcové grid rozložení.
/// </summary>
public sealed class JednaniIndexSidebarTests
{
    [Fact]
    public void JednaniIndex_ShouldUseFluidTier()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Jednani/Index.cshtml"));
        view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
            "Jednani/Index je wide-screen layout — fluid tier");
    }

    [Fact]
    public void JednaniIndex_ShouldHaveSidebarAndMeetingsAreas()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Jednani/Index.cshtml"));
        view.Should().Contain("meeting-project-overview__sidebar",
            "každá projekt-karta má levý sidebar");
        view.Should().Contain("meeting-project-overview__meetings",
            "každá projekt-karta má pravou meetings area");
    }

    [Fact]
    public void JednaniIndex_Sidebar_ShouldRenderProjektMeta()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Jednani/Index.cshtml"));
        view.Should().Contain("ProjektZkratka",
            "sidebar obsahuje zkratku projektu");
        view.Should().Contain("ProjektStav",
            "sidebar zobrazuje stav projektu");
        view.Should().Contain("PocetLetos",
            "sidebar má stat tile 'Letos'");
        view.Should().Contain("PocetCelkem",
            "sidebar má stat tile 'Celkem'");
    }

    [Fact]
    public void JednaniIndex_Sidebar_ShouldUseGovButtons()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Jednani/Index.cshtml"));
        view.Should().Contain("<pm-button",
            "akční tlačítka v sidebaru jsou pm-button (gov komponenty)");
        view.Should().NotContain("<button class=\"meeting-project-overview__action\"",
            "žádné custom button wrappery; pouze pm-button");
    }

    [Fact]
    public void JednaniIndexCss_ShouldUsePmTokens()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.meeting-project-overview\s*\{[\s\S]*?grid-template-columns\s*:\s*minmax\(\s*260px\s*,\s*300px\s*\)\s+1fr",
            "2-sloupcový grid 260-300px sidebar + fluid meetings");
        css.Should().Contain("--pm-color-primary",
            "styly využívají --pm-* tokeny (ne hardcoded barvy)");
    }

    [Fact]
    public void JednaniIndex_HistoryToggle_ShouldRemainFunctional()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Jednani/Index.cshtml"));
        view.Should().Contain("data-project-history-toggle",
            "history toggle pattern zachován (přesunut z header na button v meetings area)");
        view.Should().Contain("data-project-history-body",
            "history body wrapper zachován (JS hook na rozbalení)");
        view.Should().Contain("data-project-card",
            "parent card má data-project-card (closest lookup v JS)");
    }
}
