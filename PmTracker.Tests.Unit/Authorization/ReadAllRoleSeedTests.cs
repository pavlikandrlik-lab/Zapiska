using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ReadAllRoleSeedTests
{
    [Fact]
    public void READ_ALL_Role_ShouldBeSeeded()
    {
        PermissionSeedConfiguration.Roles
            .Should().ContainSingle(r => r.Kod == "READ_ALL");
    }

    [Fact]
    public void READ_ALL_Role_ShouldBeGlobalScope()
    {
        var role = PermissionSeedConfiguration.Roles.First(r => r.Kod == "READ_ALL");
        role.Scope.Should().Be(RoleScope.Global);
        role.IsSystem.Should().BeTrue();
    }

    [Fact]
    public void READ_ALL_Role_ShouldGrantReadingAcrossAllProjects()
    {
        var permissions = PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == "READ_ALL" && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .OrderBy(x => x)
            .ToArray();

        permissions.Should().BeEquivalentTo(new[]
        {
            "dashboard.view",
            "export.pdf",
            "export.word",
            "projects.read.all"
        }, "READ_ALL má read + export práva + projects.read.all pro management visibility");
    }
}
