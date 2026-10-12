using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Task 6 (2026-10-12): detail návrhu změny termínu a harmonogramu zobrazí červený
/// gov-infobar, když návrh mění přepínač „Automatické vyplňování harmonogramu" (atributy
/// rozhodnuté uživatelem: color="error" type="bold" closable="false" + ikona
/// components/exclamation-triangle-fill), a podbarví přepínač stejně jako ostatní změněná
/// pole návrhu. Editor návrhu nese původní stav přepínače v data-record-rezim-original pro JS
/// podbarvení při přepnutí (rezim-master-switch.js).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ScheduleProposalRezimRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public ScheduleProposalRezimRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

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
    public async Task DetailNavrhuSeZmenouRezimu_MaInfobarAPodbarvenyPrepinac()
    {
        var (projectId, subsystemId, leadId) = await SeedProjectAsync("APREZIM1");
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "Rezim1");
        // Záznam bez krok řádků = Auto; návrh přepíná na Manual.
        var proposalId = await _fixture.EnsurePendingScheduleProposalAsync(
            projectId, subsystemId, recordId, leadId, harmonogramRezim: "Manual");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/ProposalDetail?projektId={projectId}&proposalId={proposalId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var infobar = Regex.Match(html, "<gov-infobar[^>]*data-proposal-rezim-infobar[^>]*>[\\s\\S]*?</gov-infobar>").Value;
        infobar.Should().Contain("color=\"error\"").And.Contain("type=\"bold\"").And.Contain("closable=\"false\"")
            .And.Contain("name=\"exclamation-triangle-fill\"").And.Contain("slot=\"icon\"");

        Regex.Match(html, "<gov-form-switch[^>]*data-record-rezim-switch[^>]*>").Value
            .Should().Contain("proposal-field-changed").And.NotContain(" checked");
    }

    [Fact]
    public async Task DetailNavrhuBezZmenyRezimu_BezInfobaru()
    {
        var (projectId, subsystemId, leadId) = await SeedProjectAsync("APREZIM2");
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "Rezim2");
        var proposalId = await _fixture.EnsurePendingScheduleProposalAsync(projectId, subsystemId, recordId, leadId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/ProposalDetail?projektId={projectId}&proposalId={proposalId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        html.Should().NotContain("data-proposal-rezim-infobar");
    }

    /// <summary>
    /// Review fix M3 (2026-10-12): starší čekající návrh (payload bez HarmonogramRezim/
    /// ChangesHarmonogramRezim) s ručními daty pro auto-eligible krok musí renderovat infobar,
    /// i když přepínač samotný zůstává nezvýrazněný (switch beze změny — gate je
    /// ProposalRezimChangeMessage, ne ProposalChangesHarmonogramRezim).
    /// </summary>
    [Fact]
    public async Task DetailNavrhuStarySRucnimiDatyNaAutoKroku_MaInfobarBezPodbarveniPrepinace()
    {
        var (projectId, subsystemId, leadId) = await SeedProjectAsync("APREZIM4");
        // Záznam bez krok řádků = krok 3 je Auto.
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "Rezim4");

        var legacyPayloadJson = JsonSerializer.Serialize(new
        {
            ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
            SchedulePlan = new
            {
                ProjektId = projectId,
                ZaznamId = recordId,
                TerminUkonceni = DateTime.Today.AddDays(45),
                PlannedHarmonogramHodnoty = Array.Empty<object>(),
                ActualHarmonogramHodnoty = Array.Empty<object>(),
                ChangesTermDeadline = true,
                ChangesSchedulePlan = false,
                ChangesScheduleActual = true,
                ManualActualKroky = new[]
                {
                    new { Poradi = 3, AbsolutniDatum = DateOnly.FromDateTime(DateTime.Today.AddDays(10)), PreferredZdroj = (string?)null }
                }
            }
        });

        await using var dbContext = _fixture.CreateDbContext();
        var proposal = new ZaznamNavrhEntity
        {
            ProjektId = projectId,
            ZaznamId = recordId,
            SubsystemId = subsystemId,
            TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = legacyPayloadJson,
            CreatedByOsobaId = leadId,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ZaznamNavrhy.Add(proposal);
        await dbContext.SaveChangesAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/ProposalDetail?projektId={projectId}&proposalId={proposal.Id}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var infobar = Regex.Match(html, "<gov-infobar[^>]*data-proposal-rezim-infobar[^>]*>[\\s\\S]*?</gov-infobar>").Value;
        infobar.Should().NotBeEmpty();
        infobar.Should().Contain("color=\"error\"").And.Contain("type=\"bold\"").And.Contain("closable=\"false\"")
            .And.Contain("name=\"exclamation-triangle-fill\"").And.Contain("slot=\"icon\"");

        Regex.Match(html, "<gov-form-switch[^>]*data-record-rezim-switch[^>]*>").Value
            .Should().NotContain("proposal-field-changed");
    }

    [Fact]
    public async Task EditorNavrhu_PrepinacNesePuvodniStav()
    {
        var (projectId, subsystemId, leadId) = await SeedProjectAsync("APREZIM3");
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "Rezim3");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync($"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");

        Regex.Match(html, "<gov-form-switch[^>]*data-record-rezim-switch[^>]*>").Value
            .Should().Contain("data-record-rezim-original=\"Auto\"");
    }
}
