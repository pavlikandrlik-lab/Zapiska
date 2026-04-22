using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class CommentsAuthzTests
{
    [Fact]
    public void ZaznamyCommandsController_ShouldUseAuthorizePolicyAttribute_CommentsAdd()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:comments.add\")]",
            "add comment action musí mít [Authorize(Policy)] atribut místo body HasPermission checku");
    }

    [Fact]
    public void ZaznamyCommandsController_ShouldUseAuthorizePolicyAttribute_CommentsEditOwn()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:comments.edit.own\")]",
            "edit comment action musí mít [Authorize(Policy)] atribut místo body HasPermission checku");
    }

    [Fact]
    public void ZaznamyCommandsController_ShouldUseAuthorizePolicyAttribute_CommentsDeleteOwn()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:comments.delete.own\")]",
            "delete comment action musí mít [Authorize(Policy)] atribut místo body HasPermission checku");
    }
}
