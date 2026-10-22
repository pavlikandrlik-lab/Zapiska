using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Spec 2026-10-22 R13, R14: smazání plánu kroku.
/// - Uložení editoru zapíše prázdný plán (server to uměl už dřív — pojistka).
/// - Návrh změny harmonogramu nese i odstranění plánu (dřív ho mapper zahodil a návrh plán
///   „nezměnil“). Schválení plán kroku smaže a ostatní kroky nechá.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class SchedulePlanRemovalTests
{
    private readonly SqlIntegrationFixture _fixture;

    public SchedulePlanRemovalTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private sealed record Seed(int ProjectId, int RecordId, int ProposerId, int ManagerId, DateTime Start);

    /// <summary>Úkol s plánem kroku k = založení + 3·k; vedoucí subsystému a PM projektu.</summary>
    private static async Task<Seed> SeedAsync(PmTrackerDbContext dbContext, string marker)
    {
        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Lead");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Manager");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, marker.ToUpperInvariant());
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, $"{marker.ToUpperInvariant()}_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", marker);

        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var start = record.DatumZalozeni.Date;
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                PlanDatum = start.AddDays(3 * poradi),
                SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                    ? (byte)SkutecnostRezimEnum.Manual
                    : (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();
        return new Seed(projectId, recordId, proposerId, managerId, start);
    }

    /// <summary>Plán všech 10 kroků jako v záznamu, jen krok <paramref name="bezPlanu"/> prázdný.</summary>
    private static List<SaveRecordHarmonogramValueCommand> PlansWithout(Seed seed, int bezPlanu)
        => Enumerable.Range(1, 10)
            .Select(poradi => new SaveRecordHarmonogramValueCommand
            {
                Poradi = poradi,
                PlanDatum = poradi == bezPlanu ? null : seed.Start.AddDays(3 * poradi)
            })
            .ToList();

    private static async Task<Dictionary<int, DateTime?>> PlansAsync(PmTrackerDbContext dbContext, int recordId)
        => await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .ToDictionaryAsync(x => (int)x.Poradi, x => x.PlanDatum);

    [Fact]
    public async Task UlozeniEditoru_PrazdnyPlan_SmazePlanKroku()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_plan_removal_editor");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var seed = await SeedAsync(dbContext, "PlanRemEdit");
        var admin = IntegrationTestHelper.BuildUser(seed.ManagerId, isSuperAdmin: true);

        var command = IntegrationTestHelper.BuildScheduleEditCommand(store.BuildZaznamEdit(seed.RecordId));
        command.HarmonogramHodnoty = PlansWithout(seed, bezPlanu: 3);
        store.SaveRecord(command, admin);

        var plans = await PlansAsync(dbContext, seed.RecordId);
        plans[3].Should().BeNull("✕ v editoru + uložení plán kroku 3 smaže (R13)");
        plans[4].Should().Be(seed.Start.AddDays(12));
    }

    [Fact]
    public async Task NavrhOdstraniPlanKroku_PoSchvaleniJePlanPrazdny()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_plan_removal");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var seed = await SeedAsync(dbContext, "PlanRemProp");
        var (projectId, recordId, managerId, start) = (seed.ProjectId, seed.RecordId, seed.ManagerId, seed.Start);

        var proposer = IntegrationTestHelper.BuildUser(seed.ProposerId, visibleProjectIds: [projectId]);
        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var command = IntegrationTestHelper.BuildScheduleEditCommand(editor);
        command.HarmonogramHodnoty = PlansWithout(seed, bezPlanu: 3);
        store.SubmitScheduleProposal(command, proposer);

        var proposal = await dbContext.ZaznamNavrhy.AsNoTracking()
            .SingleAsync(x => x.ZaznamId == recordId && x.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange);
        var payload = JsonSerializer.Deserialize<RecordProposalPayload>(proposal.PayloadJson)!.SchedulePlan!;
        payload.PlannedHarmonogramHodnoty.Should().ContainSingle(x => x.Poradi == 3)
            .Which.PlanDatum.Should().BeNull("odstranění plánu je v návrhu (R14)");

        var manager = IntegrationTestHelper.BuildUser(
            managerId,
            visibleProjectIds: [projectId],
            grants: [IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsAccept, projectId)]);
        store.ApproveProposal(new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposal.Id }, manager);

        var plans = await PlansAsync(dbContext, recordId);
        plans[3].Should().BeNull("schválení plán kroku 3 smazalo");
        plans[2].Should().Be(start.AddDays(6));
        plans[4].Should().Be(start.AddDays(12));
    }
}
