using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Inbox úprava #10 (2026-04-21): gov-dialog variantní šířka (wide / record-editor).
///
/// Root cause (ověřeno Playwright probe v aplikačním kontextu, light-DOM, viewport 1920×1080):
///   - gov-dialog 4.2.9 dist CSS: .gov-dialog__dialog { max-width: var(--max-width, 52.5rem); max-height: 75vh }
///   - gov-dialog NEMÁ shadow DOM → inner <dialog class="gov-dialog__dialog"> je dostupný přes přímý class selector
///   - max-height je hardcoded 75vh (žádná CSS var) → override jen přes přímý selector
///
/// Fáze 2E zavedla CSS custom property --gov-dialog-max-width, kterou ale gov-dialog NEČTE → width
/// zůstával na defaultním 840px pro všechny varianty. Fix: sjednotit na --max-width.
/// </summary>
public sealed class GovDialogVariantWidthTests
{
    [Fact]
    public void SiteCss_ShouldNotDeclareObsoleteGovDialogMaxVars()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));

        // Match deklarace (--gov-dialog-max-*: value), ne zmínky v komentářích.
        css.Should().NotMatchRegex(
            @"--gov-dialog-max-width\s*:",
            "gov-dialog 4.2.9 nečte --gov-dialog-max-width — správný název je --max-width " +
            "(viz gov-design-system/styles/lib/html/components/gov-dialog.css)");
        css.Should().NotMatchRegex(
            @"--gov-dialog-max-height\s*:",
            "gov-dialog max-height je hardcoded 75vh — override jde pouze přes přímý selector " +
            ".gov-dialog__dialog { max-height: ... }");
    }

    [Fact]
    public void SiteCss_WideVariant_ShouldUseMaxWidthVar()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));

        css.Should().MatchRegex(
            @"gov-dialog\[data-modal-container\]\[data-modal-variant=""wide""\]\s*\{[^}]*--max-width\s*:\s*1100px",
            "wide varianta musí nastavit --max-width (ne --gov-dialog-max-width)");
    }

    [Fact]
    public void SiteCss_RecordEditorVariant_ShouldUseMaxWidthVarAndOverrideMaxHeight()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));

        css.Should().MatchRegex(
            @"gov-dialog\[data-modal-container\]\[data-modal-variant=""record-editor""\]\s*\{[^}]*--max-width\s*:\s*1280px",
            "record-editor varianta nastavuje --max-width: 1280px");

        css.Should().MatchRegex(
            @"gov-dialog\[data-modal-container\]\[data-modal-variant=""record-editor""\]\s+\.gov-dialog__dialog\s*\{[^}]*max-height\s*:\s*92vh",
            "record-editor potřebuje override max-height: 92vh přímo na inner .gov-dialog__dialog " +
            "(75vh default gov-dialogu nejde přes CSS var)");
    }
}
