using FluentAssertions;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ProjectRoleAuthzLinkTests
{
    [Fact]
    public void CiselnikRoliProjektuEntity_ShouldHaveAuthzRoleIdProperty()
    {
        var role = new CiselnikRoliProjektuEntity
        {
            Kod = "ADM_PROJ",
            Nazev = "Projektový admin",
            AuthzRoleId = 42
        };

        role.AuthzRoleId.Should().Be(42);
    }

    [Fact]
    public void CiselnikRoliProjektuEntity_AuthzRoleId_ShouldBeNullable()
    {
        var role = new CiselnikRoliProjektuEntity { Kod = "X", Nazev = "Y" };
        role.AuthzRoleId.Should().BeNull();
    }
}
