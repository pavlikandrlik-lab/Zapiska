using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Vyjadreni;

/// <summary>
/// Architecture guard pro 2026-04-29 dropdown redesign — náhrada drag&drop.
/// Spec: docs/superpowers/specs/2026-04-29-modal-vyjadreni-dropdown-design.md
///
/// Zachycuje invarianty:
///   - Razor obsahuje gov-form-select dropdown + gov-tag badge + ✕ clear button
///   - Buffer pro nepřiřazené kroky odstraněn
///   - CSS: drag rules pryč, krok selector styling
///   - Smazané JS moduly: stepperDragSnap.js + stepperBuffer.js
///   - Nový JS modul: bubbleStepSelector.js exportuje attachBubbleStepSelectors
///   - chatModalDragDrop.js importuje sticky + bubbleStepSelector (ne drag/buffer)
///   - BubbleViewModel má nové properties pro dropdown rendering
///   - KrokOptionViewModel record existuje
/// </summary>
public sealed class ChatModalDropdownTests
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
            throw new System.InvalidOperationException("Nepodařilo se najít kořen.");
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
    public void RazorPartial_ObsahujeBubbleStepSelectorAndClearButton()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().Contain("data-bubble-step-selector",
            "Razor musí renderovat dropdown selector v bublinách (bubbleStepSelector.js targetuje).");
        html.Should().Contain("data-clear-bubble-binding",
            "Razor musí renderovat ✕ clear button na badge.");
        html.Should().Contain("<gov-tag",
            "Razor musí používat gov-tag pro badge přiřazeného kroku.");
        html.Should().Contain("<gov-form-select",
            "Razor musí používat gov-form-select pro dropdown selektor kroků.");
    }

    [Fact]
    public void RazorPartial_ZadnyBufferProNeprirazeneKroky()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().NotContain("data-stepper-buffer",
            "Buffer pro nepřiřazené kroky byl odstraněn (kroky bez bindingu jsou implicit v dropdown options).");
        html.Should().NotContain("Nepřiřazené kroky",
            "Buffer header text odstraněn.");
    }

    [Fact]
    public void Css_ZadneDragRules()
    {
        var css = LoadRepoText("PmTracker.Web/wwwroot/css/components/chat-modal.css");
        css.Should().NotContain("cursor: grab",
            "Drag cursor odstraněn — žádný drag&drop.");
        css.Should().NotContain("data-dragging",
            "Drag state CSS odstraněn.");
        css.Should().NotContain(".pm-chat-modal__buffer",
            "Buffer CSS odstraněn.");
        css.Should().NotContain("drop-target",
            "Drop target CSS pravidlo odstraněno.");
    }

    [Fact]
    public void Css_ObsahujeKrokSelectorStyling()
    {
        var css = LoadRepoText("PmTracker.Web/wwwroot/css/components/chat-modal.css");
        css.Should().Contain(".pm-chat-bubble__krok",
            "Krok selector container styling musí existovat.");
        css.Should().Contain(".pm-chat-bubble__krok-clear",
            "✕ button styling musí existovat.");
        css.Should().Contain("cursor: pointer",
            "gov-stepper-item musí mít cursor: pointer pro click-to-scroll.");
    }

    [Fact]
    public void BubbleStepSelectorJs_Existuje()
    {
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/bubbleStepSelector.js");
        js.Should().Contain("export function attachBubbleStepSelectors",
            "Modul musí exportovat attach funkci.");
        js.Should().Contain("data-bubble-step-selector",
            "Modul musí targetovat dropdown selectory.");
        js.Should().Contain("scrollIntoView",
            "Modul musí implementovat click-to-scroll na stepper items.");
        js.Should().Contain("data-clear-bubble-binding",
            "Modul musí mít handler pro ✕ tlačítko na badge.");
    }

    [Fact]
    public void DragSnapAndBufferModules_BylySmazany()
    {
        var dragSnap = Path.Combine(FindRepoRoot(),
            "PmTracker.Web", "wwwroot", "js", "modules", "vyjadreni", "stepperDragSnap.js");
        File.Exists(dragSnap).Should().BeFalse(
            "stepperDragSnap.js byl smazán (drag broken, dropdown nepoužívá).");

        var buffer = Path.Combine(FindRepoRoot(),
            "PmTracker.Web", "wwwroot", "js", "modules", "vyjadreni", "stepperBuffer.js");
        File.Exists(buffer).Should().BeFalse(
            "stepperBuffer.js byl smazán (buffer odstraněn).");
    }

    [Fact]
    public void ChatModalDragDropJs_ImportujeNoveModuly()
    {
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/chatModalDragDrop.js");
        js.Should().Contain("import { attachBubbleStepSelectors }",
            "chatModalDragDrop musí importovat bubbleStepSelector.");
        js.Should().Contain("import { initStepperSticky }",
            "chatModalDragDrop musí zachovat stepperSticky import (informational dashboard).");
        js.Should().NotContain("stepperDragSnap",
            "chatModalDragDrop už NESMÍ importovat smazaný stepperDragSnap.");
        js.Should().NotContain("stepperBuffer",
            "chatModalDragDrop už NESMÍ importovat smazaný stepperBuffer.");
        js.Should().Contain("window.pmChatModalDragDrop",
            "Backward-compat global API zachován pro chatModal.js.");
    }

    [Fact]
    public void BubbleViewModel_MaPropertiesProDropdown()
    {
        var cs = LoadRepoText("PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs");
        cs.Should().Contain("AssignedKrokKey",
            "BublinaViewModel musí mít AssignedKrokKey property.");
        cs.Should().Contain("AssignedKrokColor",
            "BublinaViewModel musí mít AssignedKrokColor property.");
        cs.Should().Contain("AssignedKrokIsPinned",
            "BublinaViewModel musí mít AssignedKrokIsPinned property (K10 PNF auto-pinned).");
        cs.Should().Contain("StepOptions",
            "BublinaViewModel musí mít StepOptions property.");
    }

    [Fact]
    public void KrokOptionViewModel_Existuje()
    {
        var cs = LoadRepoText("PmTracker.Web/Models/ViewModels/Vyjadreni/KrokOptionViewModel.cs");
        cs.Should().Contain("public sealed record KrokOptionViewModel",
            "KrokOptionViewModel record musí existovat.");
        cs.Should().Contain("IsDisabled",
            "Record musí mít IsDisabled property pro disabled state validation.");
        cs.Should().Contain("DisabledReason",
            "Record musí mít DisabledReason property pro tooltip.");
    }

    [Fact]
    public void Builder_PocitaStepOptionsPerBubble()
    {
        var cs = LoadRepoText("PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs");
        cs.Should().Contain("ComputePerBubbleStepOptions",
            "Builder musí volat ComputePerBubbleStepOptions po načtení bublin.");
        cs.Should().Contain("BuildStepOptionsForBubble",
            "Builder musí mít helper pro per-bubble option computation.");
        cs.Should().Contain("Porušila by se chronologie",
            "Builder musí generovat tooltip pro chronologie violation.");
        cs.Should().Contain("Přiřazen bublině z",
            "Builder musí generovat tooltip pro 1:1 violation (krok bound jiné bublině).");
    }
}
