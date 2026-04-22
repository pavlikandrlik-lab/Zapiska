using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class OsobyNastaveniAuthzTests
{
    [Fact]
    public void OsobyController_ShouldUsePolicyForPeopleManageActions()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/OsobyController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:people.manage\")]",
            "OsobyController musí mít [Authorize(Policy='permission:people.manage')] na people-manage actions");
        code.Should().NotContain("CurrentUserContext.HasPermission(PermissionKeys.PeopleManage",
            "body kontroly PeopleManage musí být nahrazené policy atributem");
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
    public void NastaveniController_ShouldUseSettingsManagePolicyForMutatingActions()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/NastaveniController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:settings.manage\")]",
            "NastaveniController mutating actions musí mít [Authorize(Policy='permission:settings.manage')]");
        code.Should().NotContain("CurrentUserContext.HasPermission(PermissionKeys.SettingsManage",
            "body kontroly SettingsManage musí být nahrazené policy atributem (s výjimkou efektivni-prava sekce)");
    }
}
