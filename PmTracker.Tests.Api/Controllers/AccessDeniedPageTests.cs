using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class AccessDeniedPageTests
{
    private readonly ApiSqlFixture _fixture;

    public AccessDeniedPageTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BrowserRequest_ShouldRenderAccessDeniedPage_WhenUserIsUnknown()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Projekty?asUser=999999999");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        html.Should().Contain("access-card");
        html.Should().Contain("access-card-badge");
        html.Should().Contain("HTTP 403");
    }

    [Fact]
    public async Task AjaxRequest_ShouldKeepJsonError_WhenUserIsUnknown()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Projekty?asUser=999999999");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        payload.Should().Contain("\"error\"");
        payload.Should().NotContain("access-card");
    }
}
