using FluentAssertions;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class SubsystemRoleAuthzLinkTests
{
    [Fact]
    public void CiselnikRoleSubsystemuEntity_ShouldHaveAuthzRoleIdProperty()
    {
        var role = new CiselnikRoleSubsystemuEntity
        {
            Kod = "VEDOUCI_SUBSYSTEMU",
            Nazev = "Vedoucí subsystému",
            AuthzRoleId = 42
        };

        role.AuthzRoleId.Should().Be(42);
    }

    [Fact]
    public void CiselnikRoleSubsystemuEntity_AuthzRoleId_ShouldBeNullable()
    {
        var role = new CiselnikRoleSubsystemuEntity { Kod = "X", Nazev = "Y" };
        role.AuthzRoleId.Should().BeNull();
    }
}
