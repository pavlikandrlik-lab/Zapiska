using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Guard: tab Návrhy je lazy-loaded (data-project-tab-lazy-url). Při lazy načtení musí
/// loadProjectTabPanel zavolat initProposalFilterUi pro větev navrhy — jinak se default
/// PENDING filtr neaplikuje a uživatel vidí všechny návrhy navzdory dropdownu "Čeká na rozhodnutí".
/// </summary>
public sealed class ProposalFilterLazyInitTests
{
    [Fact]
    public void LoadProjectTabPanel_ShouldInitProposalFilterForNavrhyTab()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/projectTabs.js"));
        content.Should().Contain("initProposalFilterUi",
            "lazy-load tabu navrhy musí inicializovat proposals filtr (default PENDING + apply)");
        content.Should().Contain("tabKey === \"navrhy\"",
            "musí existovat větev pro navrhy tab v loadProjectTabPanel");
    }

    [Fact]
    public void ApplyProposalFilters_ShouldHideWholeSectionNotJustList()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/filters/index.js"));
        content.Should().Contain("section.hidden = !hasVisible",
            "prázdná sekce po filtraci musí skrýt celou .card (nadpis i list), ne osamocený nadpis");
    }

    [Fact]
    public void ApplyProjectFilterScope_ShouldApplyProposalsDirectly()
    {
        // persistFilterState (dropdown change) volá modulový handleProjectFilterInputChange
        // s prázdnými options → applyProjectFilterScope musí mít proposals větev přímo,
        // jinak se karty při změně dropdownu nepřefiltrují (options.applyScope je undefined).
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/filters/index.js"));
        var scopeFnIndex = content.IndexOf("function applyProjectFilterScope", System.StringComparison.Ordinal);
        scopeFnIndex.Should().BeGreaterThan(-1);
        var scopeFnBody = content.Substring(scopeFnIndex, 700);
        scopeFnBody.Should().Contain("scope === \"proposals\"",
            "applyProjectFilterScope musí řešit proposals scope přímo (ne přes options.applyScope)");
    }

    [Fact]
    public void HandleProjectFilterInputChange_ShouldNotPersistProposalsToSharedStorage()
    {
        // Storage klíč je sdílený napříč scopy (neobsahuje scope segment). Proposals scope
        // NESMÍ zapisovat session state — přepsalo by to records/schedule filtr v témže klíči.
        // Proposals nemá paměť (PENDING každé načtení), takže persistence je i zbytečná.
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/filters/index.js"));
        var fnIndex = content.IndexOf("function handleProjectFilterInputChange", System.StringComparison.Ordinal);
        fnIndex.Should().BeGreaterThan(-1);
        var fnBody = content.Substring(fnIndex, 400);
        fnBody.Should().Contain("scope !== \"proposals\"",
            "persistProjectFilterSessionState musí být pro proposals přeskočeno (sdílený storage klíč)");
    }

    [Fact]
    public void InitProposalFilterUi_ShouldNotClobberSharedPanelOpenPreference()
    {
        // setFilterPanelOpen zapisuje do sdíleného localStorage klíče (records+schedule+proposals).
        // initProposalFilterUi ho NESMÍ volat — panel startuje collapsed z markupu a force-write
        // by přebil uloženou preferenci otevření filtru pro records/schedule.
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/filters/index.js"));
        var fnIndex = content.IndexOf("function initProposalFilterUi", System.StringComparison.Ordinal);
        fnIndex.Should().BeGreaterThan(-1);
        var fnBody = content.Substring(fnIndex, 400);
        fnBody.Should().NotContain("setFilterPanelOpen(\"proposals\"",
            "initProposalFilterUi nesmí volat setFilterPanelOpen (přepsalo by sdílenou panel-open preferenci)");
    }
}
