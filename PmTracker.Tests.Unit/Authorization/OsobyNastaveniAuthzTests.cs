using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class OsobyNastaveniAuthzTests
{
    [Fact]
    public void OsobyController_ShouldUsePerActionPeopleKeys()
    {
        // Per-action redesign 2026-04-23: people.manage rozděleno.
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/OsobyController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:people.ad.search\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:people.create\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:people.edit\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:people.delete\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:people.ad.sync\")]");
    }

    [Fact]
    public void NastaveniController_ShouldHaveClassLevelSettingsViewPolicy()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/NastaveniController.cs"));

        code.Should().MatchRegex(
            @"\[Authorize\(Policy\s*=\s*""permission:settings\.view""\)\]\s*(\r?\n\s*)*(\[[^\]]+\]\s*(\r?\n\s*)*)*public\s+(sealed\s+)?class\s+NastaveniController",
            "NastaveniController musí mít class-level [Authorize(Policy='permission:settings.view')]");
    }

    [Fact]
    public void NastaveniController_ShouldUsePerActionSettingsKeys()
    {
        // Per-action redesign 2026-04-23: settings.manage rozděleno.
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/NastaveniController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:settings.roles.assign\")]",
            "UserRolesModal/SaveUserRole/SaveUserRolesForUser mají per-action klíč settings.roles.assign");
    }
}
