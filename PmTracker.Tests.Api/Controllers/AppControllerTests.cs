using System.Net;
using System.Text.Json;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class AppControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ApiSqlFixture _fixture;

    public AppControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task KeepAlive_ShouldReturnTokenAndTraceId_WhenUserContextIsResolved()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxGet($"/App/KeepAlive?asUser={_fixture.AdminOsobaId}");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var payload = JsonSerializer.Deserialize<KeepAliveResultViewModel>(content, JsonOptions);
        payload.Should().NotBeNull();
        payload!.Ok.Should().BeTrue();
        payload.TraceId.Should().NotBeNullOrWhiteSpace();
        payload.ServerUtc.Should().NotBeNullOrWhiteSpace();
        payload.RequestVerificationToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task KeepAlive_ShouldReturnSessionExpired_WhenUserContextCannotBeResolved()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxGet("/App/KeepAlive?asUser=99999999");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, content);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var payload = JsonSerializer.Deserialize<KeepAliveResultViewModel>(content, JsonOptions);
        payload.Should().NotBeNull();
        payload!.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("SESSION_EXPIRED");
        payload.TraceId.Should().NotBeNullOrWhiteSpace();
        payload.Message.Should().NotBeNullOrWhiteSpace();
    }
}
