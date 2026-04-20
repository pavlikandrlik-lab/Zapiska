using System.IO;
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
    public void SpecDocument_ShouldDocumentOverflowRules()
    {
        var spec = File.ReadAllText(ResolvePath("docs/specs/modal-layout-rules.md"));
        spec.Should().Contain("data-modal-scrollable",
            "spec dokumentuje opt-in flag");
        spec.Should().Contain("record-editor",
            "spec zmiňuje record-editor jako jedinou legitimní výjimku");
    }
}
