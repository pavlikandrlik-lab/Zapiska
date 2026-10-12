using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Task 4 (2026-10-12): schvalovatel vidí „Sdělení autora návrhu" na detailu návrhu změny
/// termínu a harmonogramu; schválení, které skutečně mění termín, zapíše řádek do
/// zaznam_historie_terminu (dřív to psal jen RecordService.SaveRecord). Starší návrhy
/// (payload bez DuvodZmeny) se zobrazí beze sdělení, ne s chybou.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ScheduleProposalReasonTests
{
    private readonly SqlIntegrationFixture _fixture;
    public ScheduleProposalReasonTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DetailNavrhu_UkazeSdeleniAutora()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_reason_detail");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AvailLead");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ReasonManager1");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REASON1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REASON1_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "ReasonDetail");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var command = BuildEditCommand(editor);
        command.TerminUkonceni = editor.TerminUkonceni!.Value.AddDays(14);
        command.DuvodZmeny = "Dodavatel posunul dodávku.\nNový termín potvrzen.";
        store.SubmitScheduleProposal(command, proposer);

        var proposalId = await dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ZaznamId == recordId && x.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange)
            .Select(x => x.Id)
            .SingleAsync();

        var manager = IntegrationTestHelper.BuildUser(
            managerId,
            visibleProjectIds: [projectId],
            grants: [IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsAccept, projectId)]);
        var detail = store.BuildProposalDetail(projectId, proposalId, manager);

        detail.ShowProposalAuthorMessage.Should().BeTrue();
        detail.ProposalAuthorMessage.Should().Be("Dodavatel posunul dodávku.\nNový termín potvrzen.");
        detail.ProposalAuthorName.Should().Contain("AvailLead");
    }

    [Fact]
    public async Task SchvaleniSeZmenouTerminu_ZapiseHistoriiSDuvodem()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_reason_history");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ReasonLead2");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ReasonManager2");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REASON2");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REASON2_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "ReasonHistory");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var original = editor.TerminUkonceni!.Value.Date;
        var command = BuildEditCommand(editor);
        command.TerminUkonceni = original.AddDays(14);
        command.DuvodZmeny = "Posun dodávky";
        store.SubmitScheduleProposal(command, proposer);

        var proposalId = await dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ZaznamId == recordId && x.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange)
            .Select(x => x.Id)
            .SingleAsync();

        var manager = IntegrationTestHelper.BuildUser(
            managerId,
            visibleProjectIds: [projectId],
            grants: [IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsAccept, projectId)]);
        store.ApproveProposal(new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposalId }, manager);

        var historie = await dbContext.ZaznamHistorieTerminu.AsNoTracking().SingleAsync(x => x.ZaznamId == recordId);
        historie.PuvodniDatum.Should().Be(original);
        historie.NoveDatum.Should().Be(original.AddDays(14));
        historie.Duvod.Should().Be("Posun dodávky");
    }

    [Fact]
    public async Task SchvaleniBezDuvodu_ZapiseVychoziDuvodSCislemNavrhu()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_reason_default");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ReasonLead3");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ReasonManager3");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REASON3");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REASON3_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "ReasonDefault");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var original = editor.TerminUkonceni!.Value.Date;
        var command = BuildEditCommand(editor);
        command.TerminUkonceni = original.AddDays(14);
        // Bez DuvodZmeny — výchozí důvod v historii.
        store.SubmitScheduleProposal(command, proposer);

        var proposalId = await dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ZaznamId == recordId && x.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange)
            .Select(x => x.Id)
            .SingleAsync();

        var manager = IntegrationTestHelper.BuildUser(
            managerId,
            visibleProjectIds: [projectId],
            grants: [IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsAccept, projectId)]);
        store.ApproveProposal(new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposalId }, manager);

        var historie = await dbContext.ZaznamHistorieTerminu.AsNoTracking().SingleAsync(x => x.ZaznamId == recordId);
        historie.Duvod.Should().Be($"Schválený návrh #{proposalId}");
    }

    [Fact]
    public async Task SchvaleniJenHarmonogramu_HistoriiTerminuNezapise()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_reason_noterm");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ReasonLead4");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ReasonManager4");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REASON4");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REASON4_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "ReasonNoTerm");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var start = editor.DatumZalozeni.Date;
        var command = BuildEditCommand(editor);
        // TerminUkonceni beze změny (= editor.TerminUkonceni) — jen plán kroku 1 se mění.
        command.HarmonogramHodnoty =
        [
            new SaveRecordHarmonogramValueCommand { Poradi = 1, PlanDatum = start.AddDays(10) }
        ];
        store.SubmitScheduleProposal(command, proposer);

        var proposalId = await dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ZaznamId == recordId && x.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange)
            .Select(x => x.Id)
            .SingleAsync();

        var manager = IntegrationTestHelper.BuildUser(
            managerId,
            visibleProjectIds: [projectId],
            grants: [IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsAccept, projectId)]);
        store.ApproveProposal(new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposalId }, manager);

        var historieCount = await dbContext.ZaznamHistorieTerminu.AsNoTracking().CountAsync(x => x.ZaznamId == recordId);
        historieCount.Should().Be(0, "jen harmonogram se mění, termín zůstal stejný → žádný řádek historie termínu");
    }

    [Fact]
    public async Task StaryNavrhBezDuvodu_DetailUkazeChybejiciDuvod()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_reason_legacy");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ReasonLead5");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ReasonManager5");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REASON5");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REASON5_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "ReasonLegacy");

        // Payload ve starém formátu — bez DuvodZmeny (a bez HarmonogramRezim, Task 5) — serializovaný
        // ručně jako JSON string, mimo jakýkoli aktuální DTO (simuluje návrh persistovaný dřív, než
        // pole existovalo). Záměrně bez DuvodZmeny property v JSON objektu.
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
                ChangesScheduleActual = false,
                ManualActualKroky = Array.Empty<object>()
            }
        });

        dbContext.ZaznamNavrhy.Add(new ZaznamNavrhEntity
        {
            ProjektId = projectId,
            ZaznamId = recordId,
            SubsystemId = subsystemId,
            TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = legacyPayloadJson,
            CreatedByOsobaId = proposerId,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var proposalId = await dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ZaznamId == recordId && x.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange)
            .Select(x => x.Id)
            .SingleAsync();

        var manager = IntegrationTestHelper.BuildUser(
            managerId,
            visibleProjectIds: [projectId],
            grants: [IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsAccept, projectId)]);
        var detail = store.BuildProposalDetail(projectId, proposalId, manager);

        detail.ShowProposalAuthorMessage.Should().BeTrue();
        detail.ProposalAuthorMessage.Should().BeNull();
    }

    private static SaveRecordCommand BuildEditCommand(ZaznamEditViewModel editor)
    {
        var subsystemOption = editor.Subsystemy.First(candidate =>
            string.Equals(candidate.Kod, editor.Subsystem, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Nazev, editor.Subsystem, StringComparison.OrdinalIgnoreCase));
        var subsystem = string.IsNullOrWhiteSpace(subsystemOption.Kod) ? subsystemOption.Nazev : subsystemOption.Kod;

        return new SaveRecordCommand
        {
            Id = editor.Id,
            ProjektId = editor.ProjektId,
            Kategorie = editor.Kategorie,
            TypUkolu = editor.TypUkolu,
            Stav = editor.Stav,
            Nazev = editor.Nazev,
            Cil = editor.Cil,
            Popis = editor.Popis,
            VlastnikId = editor.VlastnikId,
            DatumZalozeni = editor.DatumZalozeni,
            TerminUkonceni = editor.TerminUkonceni ?? editor.DatumZalozeni,
            Subsystem = subsystem,
            VybraniSpolupracovniciIds = editor.VybraniSpolupracovniciIds.ToList(),
            ExterniVazby = [],
            HarmonogramHodnoty = [],
            EditorTab = "schedule"
        };
    }
}
