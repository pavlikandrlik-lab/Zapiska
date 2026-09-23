using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// C2 (2026-07-10): dlaždice jednání na dashboardu = jeden flex-wrap tok
/// (kód | badge | titulek | meta) místo 3 blokových řádků.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class DashboardMeetingTileRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public DashboardMeetingTileRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> GetPanelHtmlAsync()
    {
        var leadId = await _fixture.EnsurePersonAsync("ApiDashTile");
        var projectId = await _fixture.EnsureProjectAsync("APIDASHTILE");
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureMeetingAsync(projectId, meetingNumber: 9830);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/dashboard/meetings-panel?asUser={_fixture.AdminOsobaId}");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    [Fact]
    public async Task MeetingTile_FlowsInlineWithoutTopWrapper()
    {
        var html = await GetPanelHtmlAsync();
        html.Should().NotContain("dashboard-meeting-item-top", "dlaždice jednání už nemá blokový top wrapper");
        Regex.IsMatch(html,
            "dashboard-meeting-item[\\s\\S]*?dashboard-meeting-code[\\s\\S]*?badge[\\s\\S]*?dashboard-meeting-title[\\s\\S]*?dashboard-item-meta",
            RegexOptions.Singleline).Should().BeTrue("pořadí: kód → badge → titulek → meta");
    }

    [Fact]
    public async Task MeetingTile_KeepsAllData()
    {
        var html = await GetPanelHtmlAsync();
        html.Should().Contain("Jednání č.");
        Regex.IsMatch(html, @"\d{2}\.\d{2}\.\d{4}").Should().BeTrue("datum zůstává");
        Regex.IsMatch(html, @"\d{2}:\d{2}").Should().BeTrue("čas zůstává");
    }
}
