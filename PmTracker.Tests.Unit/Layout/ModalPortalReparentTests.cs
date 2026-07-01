using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úprava #7 (2026-04-20): floating portal panel se při otevření gov-dialog
/// přemísťuje DOVNITŘ aktivního modalu, aby dědil stacking context shadow DOM
/// a byl vizuálně nad modalem (předtím se vykresloval ZA modalem).
/// </summary>
public sealed class ModalPortalReparentTests
{
    [Fact]
    public void ModalsJs_ShouldReparentFloatingRootOnOpen()
    {
        var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/modals.js"));
        js.Should().Contain("reparentFloatingRootIntoModal",
            "modals.js musí volat reparentFloatingRootIntoModal při otevření modalu");
        js.Should().Contain("floating-panel-root",
            "fix manipuluje s #floating-panel-root elementem");
    }

    [Fact]
    public void ModalsJs_ShouldRestoreFloatingRootOnClose()
    {
        var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/modals.js"));
        js.Should().Contain("restoreFloatingRoot",
            "modals.js musí volat restoreFloatingRoot před clear modalu");
    }
}
