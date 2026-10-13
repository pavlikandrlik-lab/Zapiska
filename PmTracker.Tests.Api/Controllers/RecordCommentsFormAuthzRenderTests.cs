using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Formulář vyjádření nabídne jen jednání, kde by AddComment prošel (2026-10-13) — stejná
/// CommentAuthorizationPolicy jako server. Zástupce vedoucího subsystému: jen vlastní
/// subsystém a jen DRAFT jednání (rozhodnutí 2026-06-16). Dřív u cizího subsystému editor
/// ukázal se všemi jednáními a server uložení odmítl.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordCommentsFormAuthzRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordCommentsFormAuthzRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int DeputyId, int PmId, int OwnRecordId, int ForeignRecordId, int DraftId, int OpenId);

    private async Task<Scenario> SeedAsync(string marker, int meetingBase)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var deputyId = await _fixture.EnsurePersonAsync($"{marker}Deputy");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var ownSubsystemId = await _fixture.EnsureSubsystemAsync($"{marker}A", ownerId);
        var foreignSubsystemId = await _fixture.EnsureSubsystemAsync($"{marker}B", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, ownSubsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, "PROJ_MAN");
        var ownRecordId = await _fixture.EnsureRecordAsync(projectId, ownerId, ownSubsystemId, "U", $"{marker} vlastni");
        var foreignRecordId = await _fixture.EnsureRecordAsync(projectId, ownerId, foreignSubsystemId, "U", $"{marker} cizi");
        var draftId = await _fixture.CreateMeetingAsync(projectId, "DRAFT", meetingBase);
        var openId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingBase + 1);
        return new Scenario(projectId, deputyId, pmId, ownRecordId, foreignRecordId, draftId, openId);
    }

    private async Task<string> CommentsHtmlAsync(int projectId, int recordId, int asUser)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={recordId}&asUser={asUser}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    [Fact]
    public async Task Zastupce_CiziSubsystem_FormularNenabidne()
    {
        var s = await SeedAsync("ApiCmt1", 9971);

        var html = await CommentsHtmlAsync(s.ProjectId, s.ForeignRecordId, s.DeputyId);

        html.Should().NotContain("action=\"/Zaznamy/AddComment\"", "server by uložení u cizího subsystému odmítl");
    }

    [Fact]
    public async Task Zastupce_VlastniSubsystem_NabidneJenJednaniVPriprave()
    {
        var s = await SeedAsync("ApiCmt2", 9973);

        var html = await CommentsHtmlAsync(s.ProjectId, s.OwnRecordId, s.DeputyId);

        html.Should().Contain("action=\"/Zaznamy/AddComment\"");
        html.Should().Contain($"<option value=\"{s.DraftId}\"");
        html.Should().NotContain($"<option value=\"{s.OpenId}\"", "otevřené jednání patří projektovým rolím");
    }

    [Fact]
    public async Task ProjektovyManazer_CiziSubsystem_NabidneObeJednani()
    {
        var s = await SeedAsync("ApiCmt3", 9975);

        var html = await CommentsHtmlAsync(s.ProjectId, s.ForeignRecordId, s.PmId);

        html.Should().Contain($"<option value=\"{s.DraftId}\"").And.Contain($"<option value=\"{s.OpenId}\"");
    }
}
