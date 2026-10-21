using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Plán 2026-10-21 (detail jednání se záznamy): záložka Záznamy s kontextem jednání —
/// tužka, vyjádření, Nový záznam a obnovení nesou jednání; filtry mají vlastní klíč bez výchozích.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingContextRecordsTabRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingContextRecordsTabRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int PmId, int ProjectId, int RecordId, int MeetingId)> SeedAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        var meetingId = await _fixture.EnsureMeetingAsync(projectId, 9401);
        return (pmId, projectId, recordId, meetingId);
    }

    private async Task<string> TabHtmlAsync(int projectId, int asUser, int? kontextJednaniId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var context = kontextJednaniId.HasValue ? $"&kontextJednaniId={kontextJednaniId}" : string.Empty;
        var response = await client.GetAsync($"/Projekty/RecordsTabPartial/{projectId}?asUser={asUser}{context}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    [Fact]
    public async Task WithMeetingContext_CardAndTabLinksCarryMeeting()
    {
        var s = await SeedAsync("ApiMtgTab1");

        var html = await TabHtmlAsync(s.ProjectId, s.PmId, s.MeetingId);

        html.Should().Contain($"/Zaznamy/Edit/{s.RecordId}?projektId={s.ProjectId}&jednaniId={s.MeetingId}&uiContext=meeting",
            "tužka otevře editor v kontextu jednání");
        html.Should().Contain($"/Zaznamy/RecordCommentsPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&kontextJednaniId={s.MeetingId}");
        html.Should().Contain($"data-project-tab-refresh-url=\"/Projekty/RecordsTabPartial/{s.ProjectId}?kontextJednaniId={s.MeetingId}\"");
        html.Should().Contain($"data-project-filter-storage-id=\"jednani-{s.MeetingId}\"");
        html.Should().NotContain("data-filter-save-defaults", "v jednání se výchozí filtry neukládají");
    }

    [Fact]
    public async Task WithoutContext_TabUnchanged()
    {
        var s = await SeedAsync("ApiMtgTab2");

        var html = await TabHtmlAsync(s.ProjectId, s.PmId, null);

        html.Should().NotContain("uiContext=meeting");
        html.Should().NotContain("kontextJednaniId");
        html.Should().NotContain("data-project-filter-storage-id");
        html.Should().Contain("data-filter-save-defaults");
    }

    [Fact]
    public async Task ContextOfForeignProject_IsIgnored()
    {
        var s = await SeedAsync("ApiMtgTab3");
        var other = await SeedAsync("ApiMtgTab4");

        var html = await TabHtmlAsync(s.ProjectId, s.PmId, other.MeetingId);

        html.Should().NotContain("kontextJednaniId", "jednání jiného projektu se jako kontext ignoruje");
        html.Should().NotContain("uiContext=meeting");
    }
}
