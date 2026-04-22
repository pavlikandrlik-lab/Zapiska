using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class CommentsAuthzTests
{
    [Fact]
    public void ZaznamyCommandsController_ShouldReferenceCommentsAddPermission()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().Contain("PermissionKeys.CommentsAdd",
            "add comment action musí kontrolovat comments.add permission key");
    }

    [Fact]
    public void ZaznamyCommandsController_ShouldReferenceCommentsEditOwnPermission()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().Contain("PermissionKeys.CommentsEditOwn",
            "edit comment action musí kontrolovat comments.edit.own permission key");
    }

    [Fact]
    public void ZaznamyCommandsController_ShouldReferenceCommentsDeleteOwnPermission()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().Contain("PermissionKeys.CommentsDeleteOwn",
            "delete comment action musí kontrolovat comments.delete.own permission key");
    }

    [Fact]
    public void ZaznamyCommandsController_ShouldNotContain_HasPermissionTrueStub()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().NotMatchRegex(
            @"hasPermission\s*:\s*\(\s*\)\s*=>\s*true",
            "'hasPermission: () => true' stub musí být nahrazen skutečným checkem permission key");
    }
}
