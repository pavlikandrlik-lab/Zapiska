using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Plán 2026-10-21: detail otevřeného / připravovaného jednání ukazuje záložku Záznamy
/// v kontextu jednání; uzavřené jednání jen pruh se stavem a zápis (HTML šablona tisku v iframe).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingDetailRecordsRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingDetailRecordsRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int PmId, int ProjectId, int RecordId)> SeedProjectAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        return (pmId, projectId, recordId);
    }

    private async Task<(HttpStatusCode Status, string Html)> GetAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        return (response.StatusCode, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData("OPEN", 9501)]
    [InlineData("DRAFT", 9502)]
    public async Task OpenOrDraftMeeting_ShowsRecordsInMeetingContext(string state, int number)
    {
        var s = await SeedProjectAsync($"ApiMtgDet{state}");
        var meetingId = await _fixture.CreateMeetingAsync(s.ProjectId, state, number);

        var (status, html) = await GetAsync($"/Jednani/Detail/{meetingId}?asUser={s.PmId}");

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-project-detail-root", "záložka Záznamy je v detailu jednání");
        html.Should().Contain($"jednaniId={meetingId}&uiContext=meeting", "tužka i Nový záznam nesou jednání");
        html.Should().Contain($"data-project-filter-storage-id=\"jednani-{meetingId}\"");
        html.Should().Contain("data-meeting-strip");
        html.Should().NotContain("data-meeting-minutes-frame");
    }

    [Fact]
    public async Task ClosedMeeting_ShowsMinutesFrame_WithoutRecordsAndAttendance()
    {
        var s = await SeedProjectAsync("ApiMtgDetClosed");
        var meetingId = await _fixture.CreateMeetingAsync(s.ProjectId, "CLOSED", 9503);

        var (status, html) = await GetAsync($"/Jednani/Detail/{meetingId}?asUser={s.PmId}");

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-meeting-strip");
        html.Should().Contain("data-meeting-minutes-frame");
        html.Should().NotContain("data-project-detail-root");
        html.Should().NotContain("data-meeting-attendance-card");
    }

    [Fact]
    public async Task ClosedMeeting_MinutesFrame_EmbedsPrintTemplate_WithoutAutoPrint()
    {
        // Zápis jde do iframe přes srcdoc (globální X-Frame-Options DENY by src na vlastní stránku
        // zablokovalo) — obsah = stejná šablona jako PDF tisk, bez automatického tisku.
        var s = await SeedProjectAsync("ApiMtgZapis");
        var meetingId = await _fixture.CreateMeetingAsync(s.ProjectId, "CLOSED", 9504);

        var (status, html) = await GetAsync($"/Jednani/Detail/{meetingId}?asUser={s.PmId}");

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().MatchRegex("<iframe class=\"meeting-minutes-frame\"\\s+srcdoc=\"\\s*<!DOCTYPE html>",
            "srcdoc nese celý dokument šablony tisku");
        html.Should().Contain("<div class=\"print-sheet\">");
        html.Should().NotContain("window.print()", "zápis v detailu se sám netiskne");
    }

    [Fact]
    public async Task OpenMeeting_HostRole_SeesRecordsWithoutEditorActions()
    {
        // Host (jen čtení) vidí záznamy jednání, ale ne Nový záznam, tužku ani stavové formuláře.
        var s = await SeedProjectAsync("ApiMtgDetHost");
        var hostId = await _fixture.EnsurePersonAsync("ApiMtgDetHostGuest");
        await _fixture.EnsureProjectRoleAssignmentAsync(s.ProjectId, hostId, ProjectRoleCodes.Host);
        var meetingId = await _fixture.CreateMeetingAsync(s.ProjectId, "OPEN", 9506);

        var (status, html) = await GetAsync($"/Jednani/Detail/{meetingId}?asUser={hostId}");

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-project-detail-root");
        html.Should().NotContain("data-meeting-create-record");
        html.Should().NotContain("uiContext=meeting", "bez records.edit žádný odkaz do editoru z jednání");
        html.Should().NotContain("name=\"UzavritJednani\"");
    }
}
