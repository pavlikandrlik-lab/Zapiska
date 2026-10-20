using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Middleware;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Unit.Security.ViewAs;

public sealed class ViewAsReadOnlyMiddlewareTests
{
    private sealed class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object>? Saved { get; private set; }
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => Saved = new Dictionary<string, object>(values);
    }

    private static (DefaultHttpContext Context, FakeTempDataProvider TempData) Build(string method, object? endpointMetadata = null, bool ajax = false, string? referer = null)
    {
        var tempData = new FakeTempDataProvider();
        var services = new ServiceCollection()
            .AddSingleton<ITempDataProvider>(tempData)
            .AddSingleton<ITempDataDictionaryFactory, TempDataDictionaryFactory>()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = method;
        context.Request.Host = new HostString("pmtracker.local");
        context.Response.Body = new MemoryStream();
        if (ajax) context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        if (referer is not null) context.Request.Headers.Referer = referer;
        var metadata = endpointMetadata is null ? new EndpointMetadataCollection() : new EndpointMetadataCollection(endpointMetadata);
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));
        return (context, tempData);
    }

    private static ViewAsRequestState ActiveState()
    {
        var state = new ViewAsRequestState();
        state.Activate(1, 2);
        return state;
    }

    private static ViewAsReadOnlyMiddleware Middleware(RequestDelegate next)
        => new(next, NullLogger<ViewAsReadOnlyMiddleware>.Instance);

    private static async Task<JsonElement> ReadJsonAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
    }

    [Fact]
    public async Task Inactive_PostPassesThrough()
    {
        var (context, _) = Build("POST", ajax: true);
        var called = false;

        await Middleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, new ViewAsRequestState());

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Active_GetPassesThrough_WritesStayBlocked()
    {
        var (context, _) = Build("GET");
        var state = ActiveState();
        var called = false;

        await Middleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, state);

        called.Should().BeTrue();
        state.BlocksWrites.Should().BeTrue();
    }

    [Fact]
    public async Task Active_AjaxPost_Returns403JsonWithViewAsCode_AndSkipsAction()
    {
        var (context, _) = Build("POST", ajax: true);
        var called = false;

        await Middleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, ActiveState());

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var json = await ReadJsonAsync(context);
        json.GetProperty("ok").GetBoolean().Should().BeFalse();
        json.GetProperty("errorCode").GetString().Should().Be("VIEW_AS_READ_ONLY");
        json.GetProperty("message").GetString().Should().Be(ViewAsMessages.ReadOnly);
    }

    [Fact]
    public async Task Active_FormPost_RedirectsToLocalRefererWithTempDataMessage()
    {
        var (context, tempData) = Build("POST", referer: "https://pmtracker.local/Jednani/Detail/4?asUser=1");

        await Middleware(_ => Task.CompletedTask).InvokeAsync(context, ActiveState());

        context.Response.StatusCode.Should().Be(StatusCodes.Status303SeeOther);
        context.Response.Headers.Location.ToString().Should().Be("/Jednani/Detail/4?asUser=1");
        tempData.Saved.Should().ContainKey("ErrorMessage").WhoseValue.Should().Be(ViewAsMessages.ReadOnly);
    }

    [Fact]
    public async Task Active_FormPost_ForeignReferer_RedirectsToRoot()
    {
        var (context, _) = Build("POST", referer: "https://evil.example/x");

        await Middleware(_ => Task.CompletedTask).InvokeAsync(context, ActiveState());

        context.Response.Headers.Location.ToString().Should().Be("/");
    }

    [Fact]
    public async Task Active_AllowDuringViewAsPost_Passes_ButWritesStayBlocked()
    {
        var (context, _) = Build("POST", new AllowDuringViewAsAttribute(), ajax: true);
        var state = ActiveState();
        var called = false;

        await Middleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, state);

        called.Should().BeTrue();
        state.BlocksWrites.Should().BeTrue("čtecí POST nesmí odemknout zápis");
    }

    [Fact]
    public async Task Active_ViewAsControlPost_Passes_AndPermitsWrites()
    {
        var (context, _) = Build("POST", new ViewAsControlAttribute());
        var state = ActiveState();

        await Middleware(_ => Task.CompletedTask).InvokeAsync(context, state);

        state.WritesPermitted.Should().BeTrue();
    }

    [Fact]
    public async Task Active_GetThatHitsWriteGuard_BecomesBlockedResponse()
    {
        var (context, _) = Build("GET", ajax: true);

        await Middleware(_ => throw new DbUpdateException("wrapped", new ViewAsWriteBlockedException()))
            .InvokeAsync(context, ActiveState());

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await ReadJsonAsync(context)).GetProperty("errorCode").GetString().Should().Be("VIEW_AS_READ_ONLY");
    }

    [Fact]
    public async Task Active_UnrelatedException_IsRethrown()
    {
        var (context, _) = Build("GET");

        var act = () => Middleware(_ => throw new InvalidOperationException("jiná chyba")).InvokeAsync(context, ActiveState());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("jiná chyba");
    }
}
