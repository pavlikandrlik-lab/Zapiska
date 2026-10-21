using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using Xunit;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-13 §6 — editor vykreslí volbu „Změna stavu platí k“ skrytou, s nabídkou
/// a předvybraným nejnovějším otevřeným jednáním. Kotví se na atributy: Razor kóduje diakritiku.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordEditorStatusEffectiveDateTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordEditorStatusEffectiveDateTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Edit_RendersHiddenChoice_WithNewestOpenMeetingPreselected()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiStavPlatiK");
        var projectId = await _fixture.EnsureProjectAsync("APISTAVPK");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APISTAVPKSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API stav plati k");
        // Všechna jednání fixture dává na dnešek = datum založení záznamu, spodní mez nic nevyřadí.
        var olderOpenId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9711);
        var newestOpenId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9712);
        var draftId = await _fixture.CreateMeetingAsync(projectId, "DRAFT", 9713);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Matches(html, "<div[^>]*data-status-effective-date-row[^>]*\\shidden").Should().BeTrue("volba je skrytá, dokud se stav nezmění");
        Matches(html, "<select[^>]*name=\"StavPlatiK\"").Should().BeTrue();
        Matches(html, "<option value=\"\"").Should().BeTrue("volba „dnes“ má prázdnou hodnotu");
        Matches(html, $"<option value=\"{newestOpenId}\" selected=\"selected\"").Should().BeTrue();
        Matches(html, $"<option value=\"{olderOpenId}\">").Should().BeTrue();
        Matches(html, $"<option value=\"{draftId}\">").Should().BeTrue();
    }

    [Fact]
    public async Task Edit_FromMeeting_PreselectsThatMeeting_AndReturnsToIt()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiStavPlatiKMtg");
        var projectId = await _fixture.EnsureProjectAsync("APISTAVPKMTG");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APISTAVPKMTGS", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API stav plati k z jednani");
        var olderOpenId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9721);
        var newestOpenId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9722);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit/{recordId}?projektId={projectId}&jednaniId={olderOpenId}&uiContext=meeting&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Matches(html, $"<option value=\"{olderOpenId}\" selected=\"selected\"").Should().BeTrue("editor z jednání předvybere to jednání");
        Matches(html, $"<option value=\"{newestOpenId}\">").Should().BeTrue("jiné jednání jde vybrat");
        Matches(html, $"name=\"MeetingId\" value=\"{olderOpenId}\"").Should().BeTrue("uložení se vrátí do detailu jednání");
    }

    [Fact]
    public async Task Edit_WithForeignMeetingContext_IgnoresIt()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiStavPlatiKCizi");
        var projectId = await _fixture.EnsureProjectAsync("APISTAVPKCIZI");
        var otherProjectId = await _fixture.EnsureProjectAsync("APISTAVPKCIZI2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APISTAVPKCIZIS", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API stav plati k cizi");
        var ownOpenId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9731);
        var foreignOpenId = await _fixture.CreateMeetingAsync(otherProjectId, "OPEN", 9732);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit/{recordId}?projektId={projectId}&jednaniId={foreignOpenId}&uiContext=meeting&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Matches(html, $"<option value=\"{ownOpenId}\" selected=\"selected\"").Should().BeTrue("cizí jednání se ignoruje, platí §4.3");
        Matches(html, $"name=\"MeetingId\" value=\"{foreignOpenId}\"").Should().BeFalse();
    }

    private static bool Matches(string html, string pattern)
        => Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
