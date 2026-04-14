using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class HomeObsazeniControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public HomeObsazeniControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("/Home")]
    [InlineData("/Home/Index")]
    public async Task HomeIndexRoutes_ShouldRedirectToDashboard_WhenUserIsResolved(string route)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(AppendAsUser(route, _fixture.AdminOsobaId.ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        GetLocation(response).Should().Be("/dashboard");
    }

    [Fact]
    public async Task HomeError_ShouldRenderSharedErrorPage_WhenUserIsResolved()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Home/Error?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        html.Should().Contain("Došlo k chybě");
        html.Should().Contain("Request ID:");
    }

    [Theory]
    [InlineData("/Obsazeni")]
    [InlineData("/Obsazeni/Index")]
    [InlineData("/Obsazeni?projektId=123")]
    public async Task ObsazeniRoutes_ShouldRedirectToProjectsList_ForCompatibility(string route)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(AppendAsUser(route, _fixture.AdminOsobaId.ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        GetLocation(response).Should().Be("/Projekty");
    }

    [Theory]
    [InlineData("/Home")]
    [InlineData("/Home/Error")]
    [InlineData("/Obsazeni")]
    [InlineData("/Obsazeni/Index")]
    public async Task HomeAndObsazeniRoutes_ShouldReturnForbiddenAccessPage_WhenUserContextCannotBeResolved(string route)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(AppendAsUser(route, "99999999"));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        html.Should().Contain("access-card");
    }

    private static string GetLocation(HttpResponseMessage response)
    {
        response.Headers.Location.Should().NotBeNull();
        var location = response.Headers.Location!;
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    private static string AppendAsUser(string route, string asUser)
    {
        return route.Contains('?', StringComparison.Ordinal)
            ? $"{route}&asUser={asUser}"
            : $"{route}?asUser={asUser}";
    }
}
