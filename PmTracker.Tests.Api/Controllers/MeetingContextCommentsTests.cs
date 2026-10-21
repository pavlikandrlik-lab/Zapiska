using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Plán 2026-10-21: vyjádření v detailu jednání předvybírá to jednání; jiné volby zůstávají.
/// Předvolba jen z povolené nabídky (CommentAuthorizationPolicy) a přežije obnovení po uložení.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingContextCommentsTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingContextCommentsTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int PmId, int DeputyId, int RecordId, int DraftId, int OpenId);

    private async Task<Scenario> SeedAsync(string marker, int meetingBase)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var deputyId = await _fixture.EnsurePersonAsync($"{marker}Deputy");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        var draftId = await _fixture.CreateMeetingAsync(projectId, "DRAFT", meetingBase);
        var openId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingBase + 1);
        return new Scenario(projectId, pmId, deputyId, recordId, draftId, openId);
    }

    private async Task<string> CommentsHtmlAsync(Scenario s, int asUser, int? kontextJednaniId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var context = kontextJednaniId.HasValue ? $"&kontextJednaniId={kontextJednaniId}" : string.Empty;
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={asUser}{context}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    private static bool IsSelected(string html, int meetingId) =>
        Regex.IsMatch(html, $"<option value=\"{meetingId}\" selected=\"selected\"");

    [Fact]
    public async Task ProjectManager_ContextMeeting_IsPreselected_OthersStayOffered()
    {
        var s = await SeedAsync("ApiMtgCom1", 9411);

        var html = await CommentsHtmlAsync(s, s.PmId, s.OpenId);

        IsSelected(html, s.OpenId).Should().BeTrue();
        html.Should().Contain($"<option value=\"{s.DraftId}\">", "jiné jednání jde vybrat na kliknutí");
        html.Should().Contain($"name=\"kontextJednaniId\" value=\"{s.OpenId}\"");
    }

    [Fact]
    public async Task DraftMeetingContext_PreselectsPreparation()
    {
        var s = await SeedAsync("ApiMtgCom2", 9421);

        var html = await CommentsHtmlAsync(s, s.PmId, s.DraftId);

        IsSelected(html, s.DraftId).Should().BeTrue();
    }

    [Fact]
    public async Task ContextMeetingNotAllowedForUser_NothingPreselected()
    {
        // Zástupce vedoucího smí jen DRAFT (rozhodnutí 2026-06-16) → OPEN z kontextu se nenabízí ani nepředvybere.
        var s = await SeedAsync("ApiMtgCom3", 9431);

        var html = await CommentsHtmlAsync(s, s.DeputyId, s.OpenId);

        html.Should().NotContain("selected=\"selected\"");
        html.Should().NotContain($"<option value=\"{s.OpenId}\"");
    }

    [Fact]
    public async Task WithoutContext_NothingPreselected()
    {
        var s = await SeedAsync("ApiMtgCom4", 9441);

        var html = await CommentsHtmlAsync(s, s.PmId, null);

        html.Should().NotContain("selected=\"selected\"");
        html.Should().NotContain("kontextJednaniId");
    }

    [Fact]
    public async Task AddComment_WithContext_RefreshUrlKeepsContext()
    {
        var s = await SeedAsync("ApiMtgCom5", 9451);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AddComment?projektId={s.ProjectId}&asUser={s.PmId}",
            ApiTestHttpHelper.BuildForm(
                ("ZaznamId", s.RecordId.ToString()),
                ("JednaniId", s.OpenId.ToString()),
                ("Text", "API vyjadreni z jednani"),
                ("uiContext", "project"),
                ("kontextJednaniId", s.OpenId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("record-comments");
        payload.RefreshUrl.Should().Contain($"kontextJednaniId={s.OpenId}");
    }
}
