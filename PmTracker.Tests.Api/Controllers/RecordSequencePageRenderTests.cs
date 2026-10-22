using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R10–R12: stránka záznamu se seznamem (seznam=jednani-{id} / aktivni) ukáže
/// pozici a odkazy na sousedy se stejným seznamem i návratem. Bez seznamu, s cizím jednáním,
/// s neplatnou hodnotou nebo pro záznam mimo seznam je stránka bez šipek.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordSequencePageRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordSequencePageRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Seed(int ProjectId, int MeetingId, int R1, int R2, int R3, int Closed)
    {
        public string MeetingList => $"seznam=jednani-{MeetingId}&returnUrl=%2FJednani%2FDetail%2F{MeetingId}";
    }

    /// <summary>Jednání dnes; tři běžící úkoly (pořadí = číslo záznamu) a jeden uzavřený.</summary>
    private async Task<Seed> SeedAsync(string marker, int meetingNumber)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingNumber);
        var r1 = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} prvni");
        var r2 = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} druhy");
        var r3 = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} treti");
        var closed = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} uzavreny");
        await _fixture.CloseRecordAsync(closed);
        return new Seed(projectId, meetingId, r1, r2, r3, closed);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static string Nav(string html)
        => Regex.Match(html, "<nav class=\"record-sequence-nav\"[\\s\\S]*?</nav>").Value;

    [Fact]
    public async Task MiddleRecord_ShowsPosition_AndNeighboursWithSameListAndReturn()
    {
        var s = await SeedAsync("ApiSeqPage1", 9441);

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.R2}?{s.MeetingList}&asUser={_fixture.AdminOsobaId}");

        var nav = Nav(html);
        nav.Should().Contain("2 / 3");
        nav.Should().Contain($"data-prev-url=\"/Zaznamy/Detail/{s.R1}?{s.MeetingList}\"");
        nav.Should().Contain($"data-next-url=\"/Zaznamy/Detail/{s.R3}?{s.MeetingList}\"");
        nav.Should().Contain($"href=\"/Zaznamy/Detail/{s.R1}?{s.MeetingList}\"");
        nav.Should().Contain($"href=\"/Zaznamy/Detail/{s.R3}?{s.MeetingList}\"");
        nav.Should().Contain("ApiSeqPage1 prvni", "nápověda tlačítka nese číslo a název sousedního záznamu");
        Regex.Match(html, "app-breadcrumb-back\" href=\"([^\"]+)\"").Groups[1].Value
            .Should().StartWith($"/Jednani/Detail/{s.MeetingId}", "návrat vede tam, odkud jsem přišel (R11)");
    }

    [Fact]
    public async Task EdgesOfList_DisableButton_NoWrap()
    {
        var s = await SeedAsync("ApiSeqPage2", 9442);

        var first = Nav(await GetHtmlAsync($"/Zaznamy/Detail/{s.R1}?{s.MeetingList}&asUser={_fixture.AdminOsobaId}"));
        var last = Nav(await GetHtmlAsync($"/Zaznamy/Detail/{s.R3}?{s.MeetingList}&asUser={_fixture.AdminOsobaId}"));

        first.Should().Contain("1 / 3").And.Contain("data-prev-url=\"\"");
        Regex.Match(first, "<gov-button[^>]*data-record-sequence-prev[^>]*>").Value.Should().Contain("disabled");
        last.Should().Contain("3 / 3").And.Contain("data-next-url=\"\"");
        Regex.Match(last, "<gov-button[^>]*data-record-sequence-next[^>]*>").Value.Should().Contain("disabled");
    }

    [Fact]
    public async Task ActiveList_SkipsClosedRecords()
    {
        var s = await SeedAsync("ApiSeqPage3", 9443);

        var nav = Nav(await GetHtmlAsync($"/Zaznamy/Detail/{s.R1}?seznam=aktivni&asUser={_fixture.AdminOsobaId}"));

        nav.Should().Contain("1 / 3", "uzavřený záznam do aktivních nepatří (R8)");
    }

    [Fact]
    public async Task NoList_ForeignMeeting_InvalidValue_RecordOutsideList_HaveNoArrows()
    {
        var s = await SeedAsync("ApiSeqPage4", 9444);
        var other = await SeedAsync("ApiSeqPage4b", 9445);
        var asUser = $"asUser={_fixture.AdminOsobaId}";

        (await GetHtmlAsync($"/Zaznamy/Detail/{s.R2}?{asUser}"))
            .Should().NotContain("data-record-sequence-nav", "bez parametru seznam vypadá stránka jako dřív");
        (await GetHtmlAsync($"/Zaznamy/Detail/{s.R2}?seznam=jednani-{other.MeetingId}&{asUser}"))
            .Should().NotContain("data-record-sequence-nav", "jednání jiného projektu se ignoruje");
        (await GetHtmlAsync($"/Zaznamy/Detail/{s.R2}?seznam=cokoli&{asUser}"))
            .Should().NotContain("data-record-sequence-nav");
        (await GetHtmlAsync($"/Zaznamy/Detail/{s.Closed}?seznam=aktivni&{asUser}"))
            .Should().NotContain("data-record-sequence-nav", "záznam mimo seznam šipky nemá (R12)");
        (await GetHtmlAsync($"/Zaznamy/Detail/{s.Closed}?{s.MeetingList}&{asUser}"))
            .Should().NotContain("data-record-sequence-nav", "uzavřený k jednání do něj nepatří (R12)");
    }

    [Fact]
    public async Task EditLink_ReturnsToPageWithListAndOrigin()
    {
        var s = await SeedAsync("ApiSeqPage5", 9446);

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.R2}?{s.MeetingList}&asUser={_fixture.AdminOsobaId}");

        html.Should().Contain(
            $"returnUrl=%2FZaznamy%2FDetail%2F{s.R2}%3Fseznam%3Djednani-{s.MeetingId}%26returnUrl%3D%252FJednani%252FDetail%252F{s.MeetingId}",
            "po úpravě se uživatel vrátí na stránku i se šipkami a s návratem (R11)");
    }
}
