using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard;

namespace PmTracker.Tests.Unit.ProjectDashboard;

public sealed class ProjectDashboardAuthorizationPolicyTests
{
    [Theory]
    [InlineData("PROJ_MAN")]
    [InlineData("ADM_PROJ")]
    [InlineData("GEST")]
    public void HasDashboardAccess_ShouldReturnTrue_ForAllowedRoleCodes(string roleCode)
    {
        ProjectDashboardAuthorizationPolicy.HasDashboardAccess([roleCode])
            .Should().BeTrue();
    }

    [Fact]
    public void HasDashboardAccess_ShouldReturnFalse_ForUnrelatedRole()
    {
        ProjectDashboardAuthorizationPolicy.HasDashboardAccess(["VLASTNIK_PROJEKTU", "HOST"])
            .Should().BeFalse();
    }

    [Fact]
    public void HasDashboardAccess_ShouldReturnFalse_ForEmptyRoles()
    {
        ProjectDashboardAuthorizationPolicy.HasDashboardAccess([])
            .Should().BeFalse();
    }

    [Fact]
    public void HasDashboardAccess_ShouldBeCaseInsensitive()
    {
        ProjectDashboardAuthorizationPolicy.HasDashboardAccess(["proj_man"])
            .Should().BeTrue();
    }
}
