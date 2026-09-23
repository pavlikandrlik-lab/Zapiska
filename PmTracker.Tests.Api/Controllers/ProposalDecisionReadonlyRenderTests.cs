using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>B3+B4 (2026-07-09): detail návrhu = read-only + guard-off; návrhový EDITOR guardy drží.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProposalDecisionReadonlyRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public ProposalDecisionReadonlyRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int projectId, int proposalId, int leadId)> SeedAsync()
    {
        var leadId = await _fixture.EnsurePersonAsync("ApiPropDecLead");
        var projectId = await _fixture.EnsureProjectAsync("APIPROPDEC");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIPROPDEC_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
        var proposalId = await _fixture.EnsurePendingCreateProposalAsync(projectId, subsystemId, leadId);
        return (projectId, proposalId, leadId);
    }

    [Fact]
    public async Task ProposalDetail_IsReadonly_WithGuardOff()
    {
        var (projectId, proposalId, _) = await SeedAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/ProposalDetail?projektId={projectId}&proposalId={proposalId}&asUser={_fixture.AdminOsobaId}");
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        html.Should().Contain("data-record-editor-guard=\"off\"", "B3: rozhodovací stránka nemá dirty guard");
        // B4 — spolupráce: checkboxy + search disabled (tag-scoped regex, pořadí atributů volné).
        html.Should().MatchRegex(@"<input[^>]*VybraniSpolupracovniciIds[^>]*disabled");
        html.Should().MatchRegex(@"<input[^>]*data-collab-search[^>]*disabled");
        // B4 — externí: bez add/remove akcí.
        html.Should().NotContain("data-external-add");
        html.Should().NotContain("data-external-remove");
    }

    [Fact]
    public async Task ProposalDetail_RezimSwitch_IsDisabled()
    {
        var (projectId, proposalId, _) = await SeedAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/ProposalDetail?projektId={projectId}&proposalId={proposalId}&asUser={_fixture.AdminOsobaId}");
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        // C3 (2026-07-10): switch sedí mimo panely (tabs-row), proto unikl B4 auditu.
        html.Should().MatchRegex(
            @"<gov-form-switch[^>]*data-record-rezim-switch[^>]*disabled",
            "na schvalování je vše read-only — rezim switch nesmí být editovatelný");
    }

    [Fact]
    public async Task CreateProposalEditor_RezimSwitch_StaysEditable()
    {
        var (projectId, _, leadId) = await SeedAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/CreateRecordProposal?projektId={projectId}&asUser={leadId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var switchTag = System.Text.RegularExpressions.Regex.Match(html, "<gov-form-switch[^>]*data-record-rezim-switch[^>]*>");
        if (switchTag.Success)
        {
            switchTag.Value.Should().NotContain("disabled", "v editoru návrhu zůstává switch editovatelný");
        }
        else
        {
            // Create editor switch nerenderuje (HideActual) → negativní kontrola na běžném editoru pokrytá E2E guard-on regresí.
            html.Should().NotContain("data-record-rezim-switch");
        }
    }

    [Fact]
    public async Task CreateProposalEditor_KeepsGuardOn()
    {
        var (projectId, _, leadId) = await SeedAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/CreateRecordProposal?projektId={projectId}&asUser={leadId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("data-record-editor-guard=\"off\"", "v editoru návrhu se edituje — guard musí zůstat");
        html.Should().Contain("data-external-add", "editor má add akci");
    }
}
