using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>A1 (2026-07-08): panel Účast renderuje role účastníka a grid řádky (ne tabulku).</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingAttendanceRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public MeetingAttendanceRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task MeetingDetail_AttendanceShowsRoles_InGridRows()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAttRoleOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIATTROLE");
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId); // aktivní projektová role
        var meetingId = await _fixture.EnsureMeetingAsync(projectId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/Detail/{meetingId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        html.Should().Contain("meeting-attendance-grid", "tabulku nahradil grid řádkových karet");
        html.Should().Contain("meeting-attendance-roles", "role účastníka jsou v podtextu");
        html.Should().NotContain("meeting-attendance-table");
    }

    /// <summary>D1 (2026-07-10): inline tok jméno→role→e-mail + column-major řádky ze serveru.</summary>
    [Fact]
    public async Task MeetingDetail_AttendanceRow_FlowsInline_WithRowsVariable()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAttInline");
        var projectId = await _fixture.EnsureProjectAsync("APIATTINL");
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var meetingId = await _fixture.EnsureMeetingAsync(projectId, meetingNumber: 9840);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/Detail/{meetingId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        // Pořadí v kartě: jméno (strong) → role (span) → e-mail (span); žádné blokové divy.
        html.Should().MatchRegex(
            "meeting-attendance-person[\\s\\S]{0,400}?<strong>[\\s\\S]{0,200}?<span class=\"muted meeting-attendance-roles\"");
        html.Should().NotMatchRegex("<div class=\"muted meeting-attendance-roles\"", "role už nejsou blokový podtext");
        html.Should().NotMatchRegex("<div class=\"muted\">[^<]*@", "e-mail už není blokový podtext");

        // Column-major: server posílá počet řádků = ⌈počet karet / 2⌉.
        var rowCount = System.Text.RegularExpressions.Regex.Matches(html, "class=\"meeting-attendance-row\"").Count;
        rowCount.Should().BeGreaterThan(0, "účast má aspoň jednoho člena");
        html.Should().Contain($"--attendance-rows: {(rowCount + 1) / 2}");

        // Form kontrakt beze změny.
        html.Should().Contain("rows[0].OsobaId");
    }
}
