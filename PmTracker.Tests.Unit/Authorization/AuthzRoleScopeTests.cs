using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class AuthzRoleScopeTests
{
    [Fact]
    public void AuthzRoleEntity_ShouldHaveScopeProperty()
    {
        var role = new AuthzRoleEntity
        {
            Kod = "TEST",
            Nazev = "Test",
            Scope = RoleScope.Project
        };

        role.Scope.Should().Be(RoleScope.Project);
    }

    [Fact]
    public void AuthzRoleEntity_Scope_ShouldDefaultToGlobal()
    {
        var role = new AuthzRoleEntity();
        role.Scope.Should().Be(RoleScope.Global);
    }
}
