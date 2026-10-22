using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R9, R11: obě položky „Otevřít…“ v menu karty vedou na stránku záznamu se
/// seznamem pro šipky a s návratem — z detailu jednání záznamy jednání, ze záložky Záznamy aktivní.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordCardOpenPageLinksRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordCardOpenPageLinksRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int RecordId, int MeetingId)> SeedAsync(string marker, int meetingNumber)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingNumber);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        return (projectId, recordId, meetingId);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static string Item(string html, string kind)
        => Regex.Match(html, $"<a[^>]*data-record-open-page=\"{kind}\"[^>]*>").Value;

    [Fact]
    public async Task ProjectRecordsTab_ItemsCarryActiveList()
    {
        var s = await SeedAsync("ApiOpenPage1", 9431);

        var html = await GetHtmlAsync($"/Projekty/RecordsTabPartial/{s.ProjectId}?asUser={_fixture.AdminOsobaId}");

        var href = $"href=\"/Zaznamy/Detail/{s.RecordId}?seznam=aktivni&returnUrl=%2FProjekty%2FDetail%2F{s.ProjectId}%3Ftab%3Dzaznamy";
        Item(html, "same-tab").Should().Contain(href).And.NotContain("target=");
        Item(html, "new-tab").Should().Contain(href).And.Contain("target=\"_blank\"");
        html.IndexOf("Otevřít na celou obrazovku", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Otevřít na nové kartě", StringComparison.Ordinal), "nová položka je v menu první");
    }

    [Fact]
    public async Task MeetingContext_ItemsCarryMeetingList()
    {
        var s = await SeedAsync("ApiOpenPage2", 9432);

        var html = await GetHtmlAsync(
            $"/Projekty/RecordsTabPartial/{s.ProjectId}?asUser={_fixture.AdminOsobaId}&kontextJednaniId={s.MeetingId}");

        var href = $"href=\"/Zaznamy/Detail/{s.RecordId}?seznam=jednani-{s.MeetingId}&returnUrl=%2FJednani%2FDetail%2F{s.MeetingId}\"";
        Item(html, "same-tab").Should().Contain(href);
        Item(html, "new-tab").Should().Contain(href);
    }
}
