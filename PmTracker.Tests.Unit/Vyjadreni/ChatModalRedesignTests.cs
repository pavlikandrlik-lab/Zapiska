using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Vyjadreni;

/// <summary>
/// Architecture guard pro 2026-04-28 redesign modalu Vyjádření a termíny.
/// Spec: docs/superpowers/specs/2026-04-28-modal-vyjadreni-redesign-design.md
///
/// Zachycuje invarianty:
///   - Native gov-stepper (ne custom pm-chat-stepper)
///   - CSS breakpointy 1600 / 1100 + max-width 1800
///   - Bublinky draggable=false (drag drive z kroku)
///   - Buffer pro nepřiřazené kroky
///   - Smazaný folder pm-chat-stepper
///   - Existence nových JS modulů (sticky, dragSnap, buffer)
/// </summary>
public sealed class ChatModalRedesignTests
{
    private static string LoadRepoText(string relativePath)
    {
        var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }
        if (directory is null)
        {
            throw new System.InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
        }
        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat: {full}");
        return File.ReadAllText(full);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new System.InvalidOperationException("Nepodařilo se najít kořen.");
    }

    [Fact]
    public void ChatModalCshtml_PouzivaNativeGovStepper()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().Contain("<gov-stepper",
            "Modal musí používat native gov-stepper element (ne custom pm-chat-stepper).");
        html.Should().Contain("<gov-stepper-item",
            "Modal musí obsahovat gov-stepper-item elementy.");
        html.Should().NotContain("<pm-chat-stepper",
            "Custom element pm-chat-stepper byl odstraněn — nahrazen nativním <gov-stepper>.");
    }

    [Fact]
    public void ChatModalCshtml_BublinkyNejsouDraggable()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        // Bubble li element musí mít draggable="false" (drag drive z kroku).
        html.Should().Contain("draggable=\"false\"",
            "Bublinky už nejsou draggable — drag drive z kroku přes stepperDragSnap.js.");
    }

    [Fact]
    public void ChatModalCshtml_ObsahujeBufferProNeprirazeneKroky()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().Contain("data-stepper-buffer",
            "Razor musí renderovat buffer pro nepřiřazené kroky.");
        html.Should().Contain("Nepřiřazené kroky",
            "Buffer header musí mít srozumitelný text.");
        html.Should().Contain("data-aligned-stepper",
            "Razor musí mít aligned-stepper pro přiřazené kroky.");
    }

    [Fact]
    public void ChatModalCshtml_VyuzivaColorStateProGovStepperItem()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().Contain("color=\"error\"",
            "Nepřiřazené kroky color=error (červená).");
        html.Should().Contain("\"success\"",
            "Auto kroky color=success (zelená).");
        html.Should().Contain("\"warning\"",
            "Manuální kroky color=warning (oranžová).");
    }

    [Fact]
    public void ChatModalCss_MaSpravneBreakpointy()
    {
        var css = LoadRepoText("PmTracker.Web/wwwroot/css/components/chat-modal.css");
        css.Should().Contain("max-width: min(95vw, 1800px)",
            "Modal musí mít max-width 1800px.");
        css.Should().Contain("grid-template-columns: 70fr 30fr",
            "Default grid musí být 70:30 (≥ 1600px viewport).");
        css.Should().Contain("max-width: 1599px",
            "Breakpoint pro 60:40 grid je při ≤ 1599px.");
        css.Should().Contain("grid-template-columns: 60fr 40fr",
            "Mid breakpoint má 60:40 grid.");
        css.Should().Contain("max-width: 1099px",
            "Stack breakpoint je při ≤ 1099px.");
    }

    [Fact]
    public void ChatModalCss_ZadnePmChatStepCss()
    {
        var css = LoadRepoText("PmTracker.Web/wwwroot/css/components/chat-modal.css");
        css.Should().NotContain(".pm-chat-step__poradi",
            "Custom kolečko (oval bug) bylo odstraněno — používá se native gov-stepper-item prefix slot.");
        css.Should().NotContain(".pm-chat-stepper--legacy",
            "Legacy fallback ol.pm-chat-stepper--legacy CSS bylo odstraněno.");
    }

    [Fact]
    public void PmChatStepperFolder_BylSmazan()
    {
        var pmChatStepperFolder = Path.Combine(FindRepoRoot(),
            "PmTracker.Web", "wwwroot", "js", "components", "pm-chat-stepper");
        Directory.Exists(pmChatStepperFolder).Should().BeFalse(
            "Folder pm-chat-stepper byl smazán — nahrazen native gov-stepper.");

        var pmChatStepperCss = Path.Combine(FindRepoRoot(),
            "PmTracker.Web", "wwwroot", "css", "components", "pm-chat-stepper.css");
        File.Exists(pmChatStepperCss).Should().BeFalse(
            "CSS pm-chat-stepper.css bylo smazáno.");
    }

    [Fact]
    public void Bootstrap_NeImportujePmChatStepper()
    {
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        js.Should().NotContain("pm-chat-stepper/pm-chat-stepper",
            "Bootstrap nesmí importovat smazaný pm-chat-stepper modul.");
    }

    [Fact]
    public void StepperDragSnapModule_Existuje()
    {
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperDragSnap.js");
        js.Should().Contain("findNearestBubble",
            "Modul musí mít fci pro hledání nejbližší bubliny u kurzoru (magnetic snap).");
        js.Should().Contain("export function initStepperDragSnap",
            "Exportovaná init funkce musí existovat.");
        js.Should().Contain("data-dragging",
            "Modul musí používat data-dragging atribut na taženém kroku.");
    }

    [Fact]
    public void StepperStickyModule_Existuje()
    {
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperSticky.js");
        js.Should().Contain("export function initStepperSticky",
            "Exportovaná init funkce musí existovat.");
        js.Should().Contain("ResizeObserver",
            "Sticky logika musí reagovat na resize.");
        js.Should().Contain("data-aligned-stepper",
            "Sticky logika musí targetovat aligned-stepper.");
    }

    [Fact]
    public void StepperBufferModule_Existuje()
    {
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperBuffer.js");
        js.Should().Contain("export function moveStepFromBufferToAligned",
            "Exportovaná funkce moveStepFromBufferToAligned musí existovat.");
        js.Should().Contain("export function moveStepFromAlignedToBuffer",
            "Exportovaná funkce moveStepFromAlignedToBuffer musí existovat.");
    }

    [Fact]
    public void ChatModalDragDropModule_PouzivaNoveModuly()
    {
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/chatModalDragDrop.js");
        js.Should().Contain("import { initStepperSticky }",
            "chatModalDragDrop musí importovat initStepperSticky.");
        js.Should().Contain("import { initStepperDragSnap }",
            "chatModalDragDrop musí importovat initStepperDragSnap.");
        js.Should().Contain("moveStepFromBufferToAligned",
            "chatModalDragDrop musí integrovat buffer management.");
        js.Should().Contain("window.pmChatModalDragDrop",
            "Backward-compat global API musí být zachované.");
    }

    [Fact]
    public void ViewModelBuilder_FiltrujeKrokyPerTyp()
    {
        var cs = LoadRepoText("PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs");
        cs.Should().Contain("[\"PMP\"] = new HashSet<int> { 1, 2, 3, 4, 5 }",
            "Builder musí mít hardcoded set {1,2,3,4,5} pro PMP.");
        cs.Should().Contain("[\"PNF\"] = new HashSet<int> { 1, 6, 7, 8, 9, 10 }",
            "Builder musí mít hardcoded set {1,6,7,8,9,10} pro PNF.");
        cs.Should().Contain("[\"NES\"] = new HashSet<int>()",
            "Builder musí mít prázdný set pro NES.");
        cs.Should().Contain("relevantSteps.Contains(k.KrokPoradi)",
            "Builder musí filtrovat Kroky podle relevantSteps.");
    }
}
