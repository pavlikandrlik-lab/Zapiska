using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R14: detail návrhu ukáže navržené odstranění plánu — v nápovědě
/// „Původní plán: X | Navržený: bez plánu“, pole plánu prázdné (tak, jak ho schválení zapíše).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ScheduleProposalPlanRemovalRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ScheduleProposalPlanRemovalRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DetailNavrhu_OdstraneniPlanu_UkazeBezPlanu_APrazdnePole()
    {
        var leadId = await _fixture.EnsurePersonAsync("APPLANRMLead");
        var projectId = await _fixture.EnsureProjectAsync("APPLANRM");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APPLANRM_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "APPLANRM");
        await _fixture.SetRecordFoundingDateAsync(recordId, DateTime.Today.AddDays(-40));
        var start = DateTime.Today.AddDays(-35);
        await _fixture.SeedDatumScheduleAsync(recordId, start, new HashSet<int>());
        var plan3 = start.AddDays(21);

        await using var db = _fixture.CreateDbContext();
        var proposal = new ZaznamNavrhEntity
        {
            ProjektId = projectId,
            ZaznamId = recordId,
            SubsystemId = subsystemId,
            TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = JsonSerializer.Serialize(new
            {
                ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
                SchedulePlan = new
                {
                    ProjektId = projectId,
                    ZaznamId = recordId,
                    TerminUkonceni = DateTime.Today.AddDays(30),
                    PlannedHarmonogramHodnoty = new[] { new { Poradi = 3, PlanDatum = (DateTime?)null } },
                    ActualHarmonogramHodnoty = Array.Empty<object>(),
                    ChangesSchedulePlan = true
                }
            }),
            CreatedByOsobaId = leadId,
            CreatedAt = DateTime.UtcNow
        };
        db.ZaznamNavrhy.Add(proposal);
        await db.SaveChangesAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Navrhy/ProposalDetail?projektId={projectId}&proposalId={proposal.Id}&asUser={_fixture.AdminOsobaId}");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var row = Regex.Match(html, "<tr data-schedule-step-row\\s+data-step-index=\"3\"[\\s\\S]*?</tr>").Value;
        row.Should().Contain($"Původní plán: {plan3:dd.MM.yyyy} | Navržený: bez plánu");
        Regex.Match(row, "<pm-date-field\\b[^>]*data-schedule-plan-step=\"3\"[^>]*>").Value
            .Should().Contain("iso-value=\"\"", "schválení plán smaže — detail ukazuje prázdné pole");
    }
}
