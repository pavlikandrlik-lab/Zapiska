using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Projektové menu (Option 2, 2026-07-07): server renderuje sekundární skupinu buď sbalenou
/// (3 primární + „+"; aktivní sekundární inline) nebo rozbalenou (is-expanded, bez „+") dle
/// cookie pmtracker.projectMenu.locked (čteno serverově, jako téma). Aktivní tab je vždy inline.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectMenuOverflowRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public ProjectMenuOverflowRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<int> SeedProjectAsync()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiMenuOverflowOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIMENUOVF");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIMENUOVFSUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API menu overflow record");
        return projectId;
    }

    private async Task<string> GetDetailAsync(int projectId, bool locked)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        if (locked)
        {
            client.DefaultRequestHeaders.Add("Cookie", "pmtracker.projectMenu.locked=1");
        }
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    [Fact]
    public async Task Unlocked_RendersCollapsedGroup_WithExpandToggle_AndLockToggle()
    {
        var projectId = await SeedProjectAsync();
        var html = await GetDetailAsync(projectId, locked: false);

        html.Should().Contain("data-project-menu-group");
        html.Should().NotContain("is-expanded");            // sbaleno (server)
        html.Should().Contain("data-project-menu-toggle");  // „+"
        html.Should().Contain("tab-secondary");             // sekundární tabs inline (CSS skryje neaktivní)
        html.Should().Contain("data-tab=\"tym\"");
        html.Should().Contain("data-project-menu-lock-toggle");
        html.Should().NotContain("data-project-menu-popover");
    }

    [Fact]
    public async Task Locked_RendersExpandedGroup_NoExpandToggle_ButLockToggle()
    {
        var projectId = await SeedProjectAsync();
        var html = await GetDetailAsync(projectId, locked: true);

        html.Should().Contain("data-project-menu-group");
        html.Should().Contain("is-expanded");               // server rovnou rozbalí
        html.Should().NotContain("data-project-menu-toggle"); // „+" se nerenderuje
        html.Should().Contain("data-tab=\"tym\"");
        html.Should().Contain("data-project-menu-lock-toggle");
    }
}
