using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Middleware;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Unit.Common;

public sealed class AjaxResponseContractGuardMiddlewareTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task InvokeAsync_ShouldPassThrough_WhenAjaxPostReturnsJson()
    {
        var context = BuildAjaxPostContext();
        var middleware = new AjaxResponseContractGuardMiddleware(async innerContext =>
        {
            innerContext.Response.StatusCode = StatusCodes.Status200OK;
            innerContext.Response.ContentType = "application/json; charset=utf-8";
            await innerContext.Response.WriteAsync("{\"ok\":true,\"message\":\"ok\"}");
        }, NullLogger<AjaxResponseContractGuardMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Contain("application/json");
        var rawBody = await ReadRawBodyAsync(context.Response);
        rawBody.Should().Contain("\"ok\":true");
    }

    [Fact]
    public async Task InvokeAsync_ShouldConvertEmptyAjaxResponse_ToJsonErrorWithStatus500()
    {
        var context = BuildAjaxPostContext();
        var middleware = new AjaxResponseContractGuardMiddleware(innerContext =>
        {
            innerContext.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, NullLogger<AjaxResponseContractGuardMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Contain("application/json");
        var payload = await ReadPayloadAsync(context.Response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("EMPTY_AJAX_RESPONSE");
        payload.TraceId.Should().NotBeNullOrWhiteSpace();
        payload.DiagnosticLog.Should().Contain("Request: POST /Zaznamy/Save");
        payload.DiagnosticLog.Should().Contain("Body:");
        payload.DiagnosticLog.Should().Contain("<empty>");
    }

    [Fact]
    public async Task InvokeAsync_ShouldConvertHtmlAjaxResponse_ToJsonErrorWithStatus500()
    {
        var context = BuildAjaxPostContext();
        var middleware = new AjaxResponseContractGuardMiddleware(async innerContext =>
        {
            innerContext.Response.StatusCode = StatusCodes.Status200OK;
            innerContext.Response.ContentType = "text/html; charset=utf-8";
            await innerContext.Response.WriteAsync("<html><body>Oops</body></html>");
        }, NullLogger<AjaxResponseContractGuardMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var payload = await ReadPayloadAsync(context.Response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("NON_JSON_RESPONSE");
        payload.DiagnosticLog.Should().Contain("ContentType: text/html; charset=utf-8");
        payload.DiagnosticLog.Should().Contain("<html><body>Oops</body></html>");
    }

    [Fact]
    public async Task InvokeAsync_ShouldConvertUnhandledException_ToJsonErrorWithStatus500()
    {
        var context = BuildAjaxPostContext();
        var middleware = new AjaxResponseContractGuardMiddleware(_ => throw new InvalidOperationException("boom"), NullLogger<AjaxResponseContractGuardMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var payload = await ReadPayloadAsync(context.Response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("UNEXPECTED_SERVER_ERROR");
        payload.DiagnosticLog.Should().Contain("Unhandled exception was caught by AJAX response contract guard middleware.");
        payload.DiagnosticLog.Should().Contain("InvalidOperationException: boom");
    }

    [Fact]
    public async Task InvokeAsync_ShouldBypass_WhenRequestIsNotAjaxPost()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/Zaznamy/Save";
        context.Response.Body = new MemoryStream();

        var middleware = new AjaxResponseContractGuardMiddleware(async innerContext =>
        {
            innerContext.Response.StatusCode = StatusCodes.Status200OK;
            await innerContext.Response.WriteAsync("plain");
        }, NullLogger<AjaxResponseContractGuardMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        var rawBody = await ReadRawBodyAsync(context.Response);
        rawBody.Should().Be("plain");
    }

    [Fact]
    public async Task InvokeAsync_ShouldPreserveJsonBody_WhenContentTypeIsMissing()
    {
        var context = BuildAjaxPostContext();
        var middleware = new AjaxResponseContractGuardMiddleware(async innerContext =>
        {
            innerContext.Response.StatusCode = StatusCodes.Status200OK;
            await innerContext.Response.WriteAsync("{\"ok\":false,\"message\":\"Known\",\"errorCode\":\"OPERATION_FAILED\",\"fieldErrors\":{}}", Encoding.UTF8);
        }, NullLogger<AjaxResponseContractGuardMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Contain("application/json");
        var payload = await ReadPayloadAsync(context.Response);
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Be("Known");
    }

    [Fact]
    public async Task InvokeAsync_ShouldGuardKeepAliveGet_WhenAjaxKeepAliveResponseIsEmpty()
    {
        var context = BuildAjaxGetContext("/App/KeepAlive");
        var middleware = new AjaxResponseContractGuardMiddleware(innerContext =>
        {
            innerContext.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, NullLogger<AjaxResponseContractGuardMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var payload = await ReadPayloadAsync(context.Response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("EMPTY_AJAX_RESPONSE");
        payload.DiagnosticLog.Should().Contain("Request: GET /App/KeepAlive");
    }

    private static DefaultHttpContext BuildAjaxPostContext()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = Guid.NewGuid().ToString("N");
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/Zaznamy/Save";
        context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        context.Request.Headers.Accept = "application/json";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static DefaultHttpContext BuildAjaxGetContext(string path)
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = Guid.NewGuid().ToString("N");
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        context.Request.Headers.Accept = "application/json";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadRawBodyAsync(HttpResponse response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var json = await reader.ReadToEndAsync();
        response.Body.Position = 0;
        return json;
    }

    private static async Task<ModalSubmitResultViewModel> ReadPayloadAsync(HttpResponse response)
    {
        var json = await ReadRawBodyAsync(response);
        var payload = JsonSerializer.Deserialize<ModalSubmitResultViewModel>(json, JsonOptions);
        payload.Should().NotBeNull();
        return payload!;
    }
}
