using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ProjectDashboardAuthzTests
{
    [Fact]
    public void ProjectDashboardController_ShouldUseAuthorizePolicyAttribute()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjectDashboardController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:dashboard.view\")]",
            "ProjectDashboardController musí mít [Authorize(Policy)] atribut místo body HasPermission checku");
    }

    [Fact]
    public void ProjectDashboardController_ShouldNotRelyOnIsSuperAdminBypassPattern()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjectDashboardController.cs"));

        // Starý bypass pattern: IsSuperAdmin || HasProjectRole — bez permission key kontroly
        code.Should().NotMatchRegex(
            @"CurrentUserContext\.IsSuperAdmin\s*\|\|\s*HasProjectRole",
            "starý bypass pattern musí být nahrazen explicit HasPermission check");
    }
}
