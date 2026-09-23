using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>B5 (2026-07-09): collab řádek = jméno + org celek; email jen v searchable labelu.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class CollabPanelRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public CollabPanelRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RecordEditor_CollabRows_ShowNameAndOrgUnit_NoEmailSpan()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCollabOwner");
        var projectId = await _fixture.EnsureProjectAsync("APICOLLAB");
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Create?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        html.Should().Contain("collab-option-org", "org celek se zobrazuje");
        html.Should().NotContain("collab-option-email", "email z výpisu zmizel");
        html.Should().NotContain("collab-option-origin", "stará organizace/celek dvojice zmizela");
        // searchable label dál nese email (hledání podle emailu):
        html.Should().MatchRegex(@"data-collab-label=""[^""]*@[^""]*""");
    }
}
