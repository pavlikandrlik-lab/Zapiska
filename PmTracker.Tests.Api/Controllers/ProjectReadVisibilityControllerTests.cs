using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectReadVisibilityControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public ProjectReadVisibilityControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Index_ShouldHideProject_WhenUserIsNotProjectMember()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiReadHidden");
        var projectMarker = "APIHIDE1";
        await _fixture.EnsureProjectAsync(projectMarker);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty?asUser={userId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain(projectMarker);
    }

    [Fact]
    public async Task Index_ShouldShowProject_WhenUserIsProjectMember()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiReadMember");
        var projectMarker = "APISHOW1";
        var projectId = await _fixture.EnsureProjectAsync(projectMarker);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, userId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty?asUser={userId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain(projectMarker);
    }

    [Fact]
    public async Task Detail_ShouldReturnNotFound_WhenUserCannotReadProject()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiReadDetailHidden");
        var projectId = await _fixture.EnsureProjectAsync("APIDENY1");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?asUser={userId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
