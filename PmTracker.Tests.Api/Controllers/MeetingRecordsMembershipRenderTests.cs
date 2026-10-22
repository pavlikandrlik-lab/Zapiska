using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R6: detail otevřeného / připravovaného jednání ukáže jen záznamy jednání
/// (= záznamy jeho zápisu), při načtení stránky i při obnovení záložky. Záložka Záznamy projektu
/// ukazuje dál všechny záznamy.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingRecordsMembershipRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingRecordsMembershipRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Seed(int PmId, int ProjectId, int MeetingId, int Member, int FoundedLater, int Closed);

    /// <summary>První jednání projektu (dnes): patří běžící záznam; založený zítra a uzavřený ne.</summary>
    private async Task<Seed> SeedAsync(string marker, int meetingNumber)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingNumber);
        var member = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} patri");
        var foundedLater = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} pozdeji");
        await _fixture.SetRecordFoundingDateAsync(foundedLater, DateTime.Today.AddDays(1));
        var closed = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} uzavreny");
        await _fixture.CloseRecordAsync(closed);
        return new Seed(pmId, projectId, meetingId, member, foundedLater, closed);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static void ShouldShowOnlyMember(string html, Seed s)
    {
        html.Should().Contain($"data-record-id=\"{s.Member}\"");
        html.Should().NotContain($"data-record-id=\"{s.FoundedLater}\"", "založený po dni jednání do zápisu nepatří");
        html.Should().NotContain($"data-record-id=\"{s.Closed}\"", "uzavřený k jednání (bez minulého jednání) do zápisu nepatří");
    }

    [Fact]
    public async Task MeetingDetail_ShowsOnlyMeetingRecords()
    {
        var s = await SeedAsync("ApiMtgMembers1", 9421);
        ShouldShowOnlyMember(await GetHtmlAsync($"/Jednani/Detail/{s.MeetingId}?asUser={s.PmId}"), s);
    }

    [Fact]
    public async Task RefreshInMeetingContext_ShowsOnlyMeetingRecords()
    {
        var s = await SeedAsync("ApiMtgMembers2", 9422);
        ShouldShowOnlyMember(
            await GetHtmlAsync($"/Projekty/RecordsTabPartial/{s.ProjectId}?asUser={s.PmId}&kontextJednaniId={s.MeetingId}"), s);
    }

    [Fact]
    public async Task ProjectRecordsTab_StillShowsAllRecords()
    {
        var s = await SeedAsync("ApiMtgMembers3", 9423);

        var html = await GetHtmlAsync($"/Projekty/RecordsTabPartial/{s.ProjectId}?asUser={s.PmId}");

        html.Should().Contain($"data-record-id=\"{s.Member}\"")
            .And.Contain($"data-record-id=\"{s.FoundedLater}\"")
            .And.Contain($"data-record-id=\"{s.Closed}\"");
        html.Should().NotContain("data-records-meeting-empty");
    }

    [Fact]
    public async Task MeetingWithoutRecords_ShowsEmptyState()
    {
        var s = await SeedAsync("ApiMtgMembers4", 9424);
        await _fixture.CloseRecordAsync(s.Member);

        var html = await GetHtmlAsync($"/Jednani/Detail/{s.MeetingId}?asUser={s.PmId}");

        html.Should().Contain("data-records-meeting-empty");
        html.Should().Contain("Do tohoto jednání zatím nepatří žádný záznam.");
        Regex.Matches(html, "<article class=\"record-card").Count.Should().Be(0);
    }
}
