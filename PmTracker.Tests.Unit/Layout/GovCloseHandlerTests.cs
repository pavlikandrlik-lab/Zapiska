using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úpravy #8 + #9/1 (2026-04-20): gov-close event musí korektně zavřít
/// všechny modaly. Record-editor si ponechává dirty-check flow (existing),
/// ostatní modaly fallback na přímý closeModal().
/// </summary>
public sealed class GovCloseHandlerTests
{
    [Fact]
    public void BootstrapJs_HandleGovCloseEvent_ShouldFallbackToCloseModal()
    {
        var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/bootstrap.js"));
        js.Should().Contain("handleGovCloseEvent",
            "bootstrap.js musí definovat handleGovCloseEvent");
        // Musí obsahovat fallback větev na closeModal
        js.Should().MatchRegex(
            @"handleGovCloseEvent[\s\S]*?closeModal\(\)",
            "handler obsahuje fallback volání closeModal() pro non-record-editor modaly");
    }
}
