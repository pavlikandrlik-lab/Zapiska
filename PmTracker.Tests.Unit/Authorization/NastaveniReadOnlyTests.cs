using FluentAssertions;
using PmTracker.Web.Controllers;
using Xunit;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class NastaveniReadOnlyTests
{
    [Theory]
    [InlineData("CreatePermission")]
    [InlineData("EditPermission")]
    [InlineData("DeletePermission")]
    [InlineData("CreateRole")]
    [InlineData("EditRole")]
    [InlineData("DeleteRole")]
    [InlineData("SaveRolePermission")]
    [InlineData("RemoveRolePermission")]
    [InlineData("AssignRoleProjects")]
    public void NastaveniController_ShouldNotHaveEditingAction(string actionName)
    {
        var controller = typeof(NastaveniController);
        controller.GetMethod(actionName)
            .Should().BeNull($"{actionName} musí být smazána — role/permissions se spravují přes seed");
    }
}
