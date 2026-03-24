using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Common;

public sealed class BaseControllerAuthFlowTests
{
    [Fact]
    public async Task OnActionExecutionAsync_ShouldReturnAccessDeniedPage_WhenUserIsUnauthorized()
    {
        var controller = CreateController(UserContextResolutionResult.Unauthorized("Uživatel není autentizován."));
        var context = CreateExecutingContext(controller);
        var nextInvoked = false;

        await controller.OnActionExecutionAsync(context, () =>
        {
            nextInvoked = true;
            return Task.FromResult(new ActionExecutedContext(context, [], controller));
        });

        var result = context.Result.Should().BeOfType<ViewResult>().Subject;
        result.ViewName.Should().Be("~/Views/Shared/AccessDenied.cshtml");
        context.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextInvoked.Should().BeFalse();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldKeepUnauthorizedPayload_ForAjaxRequest()
    {
        var controller = CreateController(UserContextResolutionResult.Unauthorized("Uživatel není autentizován."));
        var context = CreateExecutingContext(controller);
        context.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var nextInvoked = false;

        await controller.OnActionExecutionAsync(context, () =>
        {
            nextInvoked = true;
            return Task.FromResult(new ActionExecutedContext(context, [], controller));
        });

        var result = context.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        result.Value.Should().BeEquivalentTo(new { error = "Uživatel není autentizován." });
        nextInvoked.Should().BeFalse();
    }

    private static TestBaseController CreateController(UserContextResolutionResult resolution)
    {
        var httpContext = new DefaultHttpContext();
        var controller = new TestBaseController(new StubUserContextResolver(resolution))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return controller;
    }

    private static ActionExecutingContext CreateExecutingContext(Controller controller)
    {
        var actionContext = new ActionContext(
            controller.HttpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            controller);
    }

    private sealed class TestBaseController(IUserContextResolver userContextResolver)
        : BaseController(userContextResolver, TimeProvider.System, NullLoggerFactory.Instance);

    private sealed class StubUserContextResolver(UserContextResolutionResult resolution) : IUserContextResolver
    {
        public Task<UserContextResolutionResult> ResolveAsync(HttpContext httpContext, CancellationToken ct = default)
            => Task.FromResult(resolution);
    }
}
