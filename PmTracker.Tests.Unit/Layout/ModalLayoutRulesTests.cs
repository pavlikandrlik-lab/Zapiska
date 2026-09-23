using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úprava #9/2 (2026-04-20): modaly defaultně NEscrollovatelné.
/// User: "modaly by neměly být scrollovatelné bez opravdu závažných důvodů".
/// Výjimka: record-editor form může být delší než viewport — opt-in flag
/// data-modal-scrollable="true" nastaven automaticky v _ModalLayout.cshtml
/// pro variant="record-editor".
/// </summary>
public sealed class ModalLayoutRulesTests
{
    [Fact]
    public void SiteCss_ShouldDefaultModalOverflowToHidden()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"gov-dialog\[data-modal-container\]\s+\.modal-content\s*\{[\s\S]*?overflow\s*:\s*hidden",
            "CSS musí defaultně nastavit overflow hidden na modal-content");
    }

    [Fact]
    public void SiteCss_ShouldAllowOptInScrollable()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"data-modal-scrollable=""true""\][\s\S]*?\{[\s\S]*?overflow-y\s*:\s*auto",
            "CSS umožňuje opt-in scroll přes data-modal-scrollable flag");
    }

    [Fact]
    public void RecordEditorModal_ShouldBeOnlyOptInScrollable()
    {
        // Flag je nastavován automaticky přes _ModalLayout.cshtml (podmíněný
        // atribut pro variant="record-editor") — ne přímo v _EditZaznamForm.cshtml.
        var modalLayout = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_ModalLayout.cshtml"));
        modalLayout.Should().Contain("data-modal-scrollable",
            "record-editor form (delší než viewport) je legitimní výjimka z scroll policy; " +
            "_ModalLayout.cshtml nastavuje opt-in flag pro variant=record-editor");
        modalLayout.Should().Contain("record-editor",
            "_ModalLayout.cshtml musí rozlišovat variant record-editor pro opt-in scroll flag");
    }

    [Fact]
    public void SiteCss_FloatingPanel_ShouldStackAboveModalRoot_ButBelowRecordEditorCloseGuard()
    {
        // Bug 2026-07-04: floating pickery (person/AD/date/time) se mountují do #floating-panel-root
        // v <body> (mimo gov-dialog kvůli transform containing-block). Tím ztratí z-index z pravidla
        // `gov-dialog … .office-search-panel { z-index: 2600 }` a spadnou pod modal (.modal-root:2000),
        // takže výsledky se vykreslí ZA modalem. Invariant: .floating-panel > .modal-root, ale < close-guard.
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        var noComments = Regex.Replace(css, @"/\*[\s\S]*?\*/", string.Empty);

        int ZIndexOf(string selector)
        {
            var match = Regex.Match(noComments, Regex.Escape(selector) + @"\s*\{[^}]*?z-index\s*:\s*(\d+)");
            match.Success.Should().BeTrue($"pravidlo {selector} musí definovat z-index");
            return int.Parse(match.Groups[1].Value);
        }

        var floatingPanel = ZIndexOf(".floating-panel");
        var modalRoot = ZIndexOf(".modal-root");
        var closeGuard = ZIndexOf(".record-editor-close-guard");

        floatingPanel.Should().BeGreaterThan(modalRoot,
            "floating dropdown panely musí být NAD modalem, jinak se výsledky vykreslí za ním");
        floatingPanel.Should().BeLessThan(closeGuard,
            "floating panel zůstává POD record-editor close-guardem");
    }

    [Fact]
    public void SpecDocument_ShouldDocumentOverflowRules()
    {
        var spec = File.ReadAllText(ResolvePath("docs/specs/modal-layout-rules.md"));
        spec.Should().Contain("data-modal-scrollable",
            "spec dokumentuje opt-in flag");
        spec.Should().Contain("record-editor",
            "spec zmiňuje record-editor jako jedinou legitimní výjimku");
    }
}
