using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Modals;

/// <summary>
/// Fáze 2E: _ModalLayout přechází z custom .modal-overlay na &lt;gov-dialog&gt;.
/// Data-modal-* kontrakty (container, close, variant) musí zůstat pro kompatibilitu
/// s existujícím JS (modals.js, ajax.js, bootstrap.js) a 18 views.
/// </summary>
public sealed class ModalLayoutMarkupTests
{
    private static string LoadText(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat na cestě {full}");
        return File.ReadAllText(full);
    }

    [Fact]
    public void Layout_ShouldRenderGovDialogRoot()
    {
        var layout = LoadText("PmTracker.Web/Views/Shared/_ModalLayout.cshtml");

        layout.Should().Contain(
            "<gov-dialog",
            "root element modálu je <gov-dialog> (Fáze 2E)");
        layout.Should().NotContain(
            "class=\"modal-overlay\"",
            "legacy custom overlay byl odstraněn");
        layout.Should().NotContain(
            "<div class=\"modal modal--",
            "legacy .modal.modal--* variant wrapper byl odstraněn");
    }

    [Fact]
    public void Layout_ShouldPreserveDataModalContracts()
    {
        var layout = LoadText("PmTracker.Web/Views/Shared/_ModalLayout.cshtml");

        layout.Should().Contain(
            "data-modal-container",
            "JS používá [data-modal-container] selector (modals.js, ajax.js)");
        layout.Should().NotContain(
            "data-modal-close",
            "Task 5: vlastní X button odstraněn (gov-dialog má vestavěný); [data-modal-close] contract zůstává v _ModalFormActions + per-view partials");
        layout.Should().Contain(
            "data-modal-variant=\"@normalizedVariant\"",
            "variant se propaguje jako data-modal-variant atribut (místo CSS class)");
    }

    [Fact]
    public void Layout_ShouldMoveFloatingRootOutOfDialog()
    {
        var layout = LoadText("PmTracker.Web/Views/Shared/_ModalLayout.cshtml");

        // Floating root je přesunut do _Layout.cshtml (#floating-panel-root) v Task 3.
        // Gov-dialog má shadow DOM a není vhodný host pro floating pickery.
        layout.Should().NotContain(
            "data-modal-floating-root",
            "floating-root přemístěn mimo gov-dialog (Task 3)");
    }

    [Fact]
    public void Bootstrap_ShouldListenForGovCloseEvent()
    {
        var js = LoadText("PmTracker.Web/wwwroot/js/modules/bootstrap.js");

        js.Should().Contain(
            "\"gov-close\"",
            "bootstrap.js musí poslouchat gov-dialog emitted gov-close event (vestavěný X)");
        js.Should().Contain(
            "handleGovCloseEvent",
            "musí existovat handler pro gov-close");
    }
}
