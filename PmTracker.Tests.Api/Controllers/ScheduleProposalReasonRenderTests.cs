using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Task 3+4 (2026-10-12): pole „Důvod změny" v editoru návrhu změny termínu a harmonogramu
/// (jen tam, ne v klasickém editoru záznamu) a jeho zobrazení schvalovateli na detailu návrhu
/// („Sdělení autora návrhu"). Razor kóduje diakritiku na entity → kotvíme se na atributy/ASCII;
/// český text se ověřuje na view modelu v Integration testech.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ScheduleProposalReasonRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public ScheduleProposalReasonRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int projectId, int subsystemId, int leadId)> SeedProjectAsync(string marker)
    {
        var leadId = await _fixture.EnsurePersonAsync($"{marker}Lead");
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
        return (projectId, subsystemId, leadId);
    }

    [Fact]
    public async Task EditorNavrhu_MaPoleDuvodu_EditorZaznamuNe()
    {
        var (projectId, subsystemId, leadId) = await SeedProjectAsync("APREASON1");
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "Reason1");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var proposalResponse = await client.GetAsync($"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var proposalHtml = await proposalResponse.Content.ReadAsStringAsync();
        proposalResponse.StatusCode.Should().Be(HttpStatusCode.OK, proposalHtml);
        proposalHtml.Should().Contain("name=\"DuvodZmeny\"").And.Contain("maxlength=\"1000\"").And.Contain("data-record-proposal-reason");

        var editResponse = await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        var editHtml = await editResponse.Content.ReadAsStringAsync();
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK, editHtml);
        editHtml.Should().NotContain("name=\"DuvodZmeny\"");
    }
}
