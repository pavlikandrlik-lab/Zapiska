using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Bug 2026-07-04: při text-selection dragu z inputu v modalu (např. „nové jednání",
/// okénko pro číslo záznamu) ven na ztmavené pozadí (backdrop) a puštění tlačítka myši
/// tam se zavřel celý modal. Příčina: gov-dialog vyplňuje viewport a vycentrovaný obsah
/// je uvnitř; po drag-selectu se `click` retargetuje na společného předka (gov-dialog),
/// a handleDocumentClick backdrop-close větev to vyhodnotila jako kliknutí na pozadí →
/// closeModal().
///
/// Fix: modal se zavírá jen explicitní akcí — křížek (gov-close), tlačítka
/// data-modal-close, Escape. _ModalLayout.cshtml navíc už deklaruje
/// block-backdrop-close="true", takže JS backdrop-close větev byla v rozporu se šablonou.
/// handleDocumentClick proto NESMÍ zavírat modal na gov-dialog (backdrop) kliknutí.
/// </summary>
public sealed class ModalBackdropCloseTests
{
    [Fact]
    public void HandleDocumentClick_MustNotCloseModalOnBackdropClick()
    {
        var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/bootstrap.js"));
        var body = ExtractFunctionBody(js, "function handleDocumentClick(event) {");

        body.Should().NotContain("GOV-DIALOG",
            "handleDocumentClick nesmí mít backdrop-close větev — click na gov-dialog (pozadí) " +
            "vzniká i při text-selection dragu z obsahu modalu ven a nesmí modal zavírat");
    }

    [Fact]
    public void ModalLayout_DeclaresBlockBackdropClose()
    {
        var cshtml = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_ModalLayout.cshtml"));
        cshtml.Should().Contain("block-backdrop-close=\"true\"",
            "šablona modalu deklaruje, že backdrop nemá modal zavírat — JS to nesmí obcházet");
    }

    [Fact]
    public void HandleGovCloseEvent_StillClosesModal_ViaCloseButton()
    {
        // Regrese guard: fix nesmí odstranit legitimní close cestu přes křížek (gov-close).
        var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/bootstrap.js"));
        var body = ExtractFunctionBody(js, "function handleGovCloseEvent(event) {");
        body.Should().Contain("closeModal()",
            "křížek (gov-close) musí modal dál zavírat");
    }

    private static string ExtractFunctionBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"zdroj musí obsahovat {signature}");

        // Tělo končí na začátku další top-level funkce (řádek začínající "function "
        // nebo "export function ").
        var searchFrom = start + signature.Length;
        var nextFn = source.IndexOf("\nfunction ", searchFrom, StringComparison.Ordinal);
        var nextExportFn = source.IndexOf("\nexport function ", searchFrom, StringComparison.Ordinal);
        var end = new[] { nextFn, nextExportFn }
            .Where(i => i >= 0)
            .DefaultIfEmpty(source.Length)
            .Min();
        return source.Substring(start, end - start);
    }
}
