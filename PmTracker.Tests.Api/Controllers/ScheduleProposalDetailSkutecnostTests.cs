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
/// Spec 2026-10-14 — detail návrhu změny harmonogramu musí schvalovateli ukázat skutečnost tak,
/// jak ji schválení zapíše: navržené smazání jako prázdné pole (zvýrazněné), a při přepnutí na
/// automatiku bez dat, která automat nedodal (R2 — po schválení se smažou).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ScheduleProposalDetailSkutecnostTests
{
    private static readonly DateTime A = new(2026, 10, 5);
    private static readonly DateTime B = new(2026, 10, 9);
    private readonly ApiSqlFixture _fixture;

    public ScheduleProposalDetailSkutecnostTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int SubsystemId, int LeadId, int RecordId)> ZalozUkolAsync(string marker)
    {
        var leadId = await _fixture.EnsurePersonAsync($"{marker}Lead");
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", marker);
        await using var db = _fixture.CreateDbContext();
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                    ? (byte)SkutecnostRezimEnum.Manual
                    : (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return (projectId, subsystemId, leadId, recordId);
    }

    private async Task NastavKrokAsync(int recordId, int poradi, DateTime? datum, SkutecnostZdrojEnum zdroj, SkutecnostRezimEnum rezim)
    {
        await using var db = _fixture.CreateDbContext();
        var k = await db.ZaznamHarmonogramKroky.SingleAsync(x => x.ZaznamId == recordId && x.Poradi == poradi);
        k.SkutecnostDatum = datum;
        k.SkutecnostZdroj = (byte)zdroj;
        k.SkutecnostRezim = (byte)rezim;
        await db.SaveChangesAsync();
    }

    private async Task<string> DetailAsync((int ProjectId, int SubsystemId, int LeadId, int RecordId) z, object schedulePlan)
    {
        await using var db = _fixture.CreateDbContext();
        var proposal = new ZaznamNavrhEntity
        {
            ProjektId = z.ProjectId,
            ZaznamId = z.RecordId,
            SubsystemId = z.SubsystemId,
            TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = JsonSerializer.Serialize(new
            {
                ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
                SchedulePlan = schedulePlan
            }),
            CreatedByOsobaId = z.LeadId,
            CreatedAt = DateTime.UtcNow
        };
        db.ZaznamNavrhy.Add(proposal);
        await db.SaveChangesAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Navrhy/ProposalDetail?projektId={z.ProjectId}&proposalId={proposal.Id}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    private static string Radek(string html, int poradi)
        => Regex.Match(html, $"<tr data-schedule-step-row\\s+data-step-index=\"{poradi}\"[\\s\\S]*?</tr>").Value;

    private static string BunkaSkutecnosti(string radek)
        => radek[radek.LastIndexOf("<td", StringComparison.Ordinal)..];

    [Fact]
    public async Task NavrzeneSmazani_DetailUkazePrazdnouZvyraznenouSkutecnost()
    {
        var z = await ZalozUkolAsync("APDETSK1");
        await NastavKrokAsync(z.RecordId, 5, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);

        var html = await DetailAsync(z, new
        {
            ProjektId = z.ProjectId,
            ZaznamId = z.RecordId,
            TerminUkonceni = new DateTime(2026, 12, 31),
            PlannedHarmonogramHodnoty = Array.Empty<object>(),
            ActualHarmonogramHodnoty = new[] { new { Poradi = 5, SkutecnostDatum = (DateTime?)null } },
            ChangesScheduleActual = true
        });

        var bunka = BunkaSkutecnosti(Radek(html, 5));
        // Původní datum smí zůstat jen v tooltipu „Původní … | Navržená …“, ne v zobrazené hodnotě.
        Regex.Match(bunka, "data-manual-krok-readonly[^>]*>([^<]*)<").Groups[1].Value
            .Should().NotContain("05.10.2026", "návrh datum kroku 5 maže");
        bunka.Should().Contain("proposal-field-changed", "změněná skutečnost se zvýrazní");
    }

    [Fact]
    public async Task NavrhNaAutomatiku_DetailNeukazujeDataKteraSchvaleniSmaze()
    {
        var z = await ZalozUkolAsync("APDETSK2");
        await NastavKrokAsync(z.RecordId, 3, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);
        await NastavKrokAsync(z.RecordId, 4, B, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Manual);

        var html = await DetailAsync(z, new
        {
            ProjektId = z.ProjectId,
            ZaznamId = z.RecordId,
            TerminUkonceni = new DateTime(2026, 12, 31),
            PlannedHarmonogramHodnoty = Array.Empty<object>(),
            ActualHarmonogramHodnoty = Array.Empty<object>(),
            HarmonogramRezim = "Auto",
            ChangesHarmonogramRezim = true
        });

        var krok3 = Radek(html, 3);
        krok3.Should().NotContain("05.10.2026", "ruční datum se přepnutím na automatiku smaže (R2)");
        krok3.Should().Contain("data-skutecnost-rezim=\"Auto\"", "buňky ukazují režim z návrhu");
        Radek(html, 4).Should().Contain("09.10.2026", "data automatu zůstávají");
    }
}
