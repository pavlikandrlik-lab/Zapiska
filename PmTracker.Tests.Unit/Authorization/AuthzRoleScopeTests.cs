using FluentAssertions;
using PmTracker.Web.Models.Entities;

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
            Scope = "PROJECT"
        };

        role.Scope.Should().Be("PROJECT");
    }

    [Fact]
    public void AuthzRoleEntity_Scope_ShouldDefaultToGlobal()
    {
        var role = new AuthzRoleEntity();
        role.Scope.Should().Be("GLOBAL");
    }
}
