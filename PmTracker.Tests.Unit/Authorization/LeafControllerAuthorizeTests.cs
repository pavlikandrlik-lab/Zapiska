using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class LeafControllerAuthorizeTests
{
    [Theory]
    [InlineData("HomeController.cs")]
    [InlineData("ProfilController.cs")]
    [InlineData("DokumentaceController.cs")]
    [InlineData("DashboardController.cs")]
    public void LeafController_ShouldHaveClassLevelAuthorize(string fileName)
    {
        var code = File.ReadAllText(ResolvePath($"PmTracker.Web/Controllers/{fileName}"));

        // Class-level [Authorize] attribute (no policy) — authenticated user only
        code.Should().MatchRegex(
            @"\[Authorize\](\s|\r|\n)+public\s+(sealed\s+)?(partial\s+)?class\s+\w+Controller",
            $"{fileName} musí mít class-level [Authorize] atribut");
    }

    [Fact]
    public void StyleGuideController_ShouldRemain_WithoutAuthorizeAttribute()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/StyleGuideController.cs"));

        // StyleGuide explicitně NESMÍ mít [Authorize] — má code comment zakazující to
        code.Should().NotMatchRegex(
            @"\[Authorize\](\s|\r|\n)+public\s+(sealed\s+)?class\s+StyleGuideController",
            "StyleGuide zůstává bez [Authorize] kvůli IIS/Kestrel problému (viz komentář v souboru)");
    }
}
