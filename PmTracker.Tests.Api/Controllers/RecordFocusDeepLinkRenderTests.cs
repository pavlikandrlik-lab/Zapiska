using System.Net;
using System.Text.Encodings.Web;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Po založení / editaci / schválení návrhu se přesměruje na Detail projektu s ?recordId=&lt;id&gt;,
/// aby se na záložce Záznamy sjelo na záznam a vysvítil se (deep-link). Server: recordId → TargetRecordId
/// jen na záložce Záznamy (jinde se ignoruje).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordFocusDeepLinkRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public RecordFocusDeepLinkRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int projectId, int recordId)> SeedProjectWithRecordAsync()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiFocusOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIFOCUS");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIFOCUSSUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Api focus record");
        return (projectId, recordId);
    }

    private async Task<string> GetDetailAsync(int projectId, string tab, int recordId, string extraQuery = "")
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab={tab}&recordId={recordId}{extraQuery}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    [Fact]
    public async Task RecordsTab_WithRecordId_RendersDeepLinkTarget()
    {
        var (projectId, recordId) = await SeedProjectWithRecordAsync();
        var html = await GetDetailAsync(projectId, "zaznamy", recordId);

        html.Should().Contain($"data-record-target-id=\"{recordId}\"",
            "na záložce Záznamy se recordId promítne do deep-link cíle (sjede + vysvítí)");
    }

    [Fact]
    public async Task ProposalsTab_WithRecordId_DoesNotRenderDeepLinkTarget()
    {
        var (projectId, recordId) = await SeedProjectWithRecordAsync();
        var html = await GetDetailAsync(projectId, "navrhy", recordId);

        html.Should().NotContain($"data-record-target-id=\"{recordId}\"",
            "deep-link cíl je vázán jen na záložku Záznamy — na jiné se recordId ignoruje");
    }

    [Fact]
    public async Task RecordsTab_ZVysledkuHledani_PredaVyjadreniAHledanyTextKarte()
    {
        var (projectId, recordId) = await SeedProjectWithRecordAsync();
        var html = await GetDetailAsync(projectId, "zaznamy", recordId, "&vyjadreniId=987&hl=z%C3%A1loha%20dat");

        var encoder = _fixture.Factory.Services.GetRequiredService<HtmlEncoder>();
        html.Should().Contain("data-record-target-comment-id=\"987\"", "detail otevře vyjádření se shodou");
        html.Should().Contain($"data-record-target-highlight=\"{encoder.Encode("záloha dat")}\"", "a podsvítí hledaný text");
    }

    [Fact]
    public async Task ProposalsTab_ZVysledkuHledani_NepredavaCilVyjadreni()
    {
        var (projectId, recordId) = await SeedProjectWithRecordAsync();
        var html = await GetDetailAsync(projectId, "navrhy", recordId, "&vyjadreniId=987&hl=zaloha");

        html.Should().NotContain("data-record-target-comment-id=\"987\"");
        html.Should().NotContain("data-record-target-highlight=\"zaloha\"");
    }
}

