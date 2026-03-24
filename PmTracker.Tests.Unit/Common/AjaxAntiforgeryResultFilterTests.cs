using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Filters;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Unit.Common;

public sealed class AjaxAntiforgeryResultFilterTests
{
    [Fact]
    public async Task OnResultExecutionAsync_ShouldReturnJsonPayload_ForAjaxAntiforgeryFailure()
    {
        var sut = new AjaxAntiforgeryResultFilter(NullLogger<AjaxAntiforgeryResultFilter>.Instance);
        var actionContext = BuildActionContext(isAjax: true);
        var originalResult = new FakeAntiforgeryValidationFailedResult();
        var context = new ResultExecutingContext(
            actionContext,
            [],
            originalResult,
            controller: new object());
        var nextCalled = false;

        await sut.OnResultExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ResultExecutedContext(actionContext, [], originalResult, new object()));
        });

        nextCalled.Should().BeFalse();
        context.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)context.Result;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var payload = badRequest.Value.Should().BeOfType<ModalSubmitResultViewModel>().Subject;
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("REQUEST_VALIDATION_FAILED");
        payload.Message.Should().Contain("token formuláře");
        payload.TraceId.Should().NotBeNullOrWhiteSpace();
        payload.FieldErrors.Keys.Should().Contain("__RequestVerificationToken");
    }

    [Fact]
    public async Task OnResultExecutionAsync_ShouldPassThrough_ForNonAjaxRequest()
    {
        var sut = new AjaxAntiforgeryResultFilter(NullLogger<AjaxAntiforgeryResultFilter>.Instance);
        var actionContext = BuildActionContext(isAjax: false);
        var originalResult = new FakeAntiforgeryValidationFailedResult();
        var context = new ResultExecutingContext(
            actionContext,
            [],
            originalResult,
            controller: new object());
        var nextCalled = false;

        await sut.OnResultExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ResultExecutedContext(actionContext, [], originalResult, new object()));
        });

        nextCalled.Should().BeTrue();
        context.Result.Should().BeSameAs(originalResult);
    }

    private static ActionContext BuildActionContext(bool isAjax)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = Guid.NewGuid().ToString("N");
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/Zaznamy/Save";
        httpContext.Request.Headers["Accept"] = "application/json";
        if (isAjax)
        {
            httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        }

        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    }

    private sealed class FakeAntiforgeryValidationFailedResult : IAntiforgeryValidationFailedResult
    {
        public Task ExecuteResultAsync(ActionContext context) => Task.CompletedTask;
    }
}
