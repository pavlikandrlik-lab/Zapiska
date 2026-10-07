using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>A4 (2026-07-08): stránky, kde breadcrumbs chyběly, je renderují.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class BreadcrumbCoverageTests
{
    private readonly ApiSqlFixture _fixture;
    public BreadcrumbCoverageTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> GetOkAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Razor enkóduje diakritiku (&#xFD;…) → dekódovat, ať asserty čtou český text.
        return System.Net.WebUtility.HtmlDecode(html);
    }

    [Fact]
    public async Task SearchPage_HasNoBreadcrumbBar()
    {
        // Uživatel 2026-10-07: Hledání je zanoření 0, lištu nemá.
        var html = await GetOkAsync($"/Search?q=test&asUser={_fixture.AdminOsobaId}");
        html.Should().NotContain("class=\"app-breadcrumb-bar\"");
    }

    [Fact]
    public async Task CreateRecordProposalPage_RendersProjectBreadcrumbs()
    {
        // Návrh smí založit jen vedoucí subsystému (service gate) → seed lead + asUser=lead.
        var leadId = await _fixture.EnsurePersonAsync("ApiBcLead");
        var projectId = await _fixture.EnsureProjectAsync("APIBC");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIBC_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);

        var html = await GetOkAsync($"/Navrhy/CreateRecordProposal?projektId={projectId}&asUser={leadId}");
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("Nový návrh záznamu");
    }
}
