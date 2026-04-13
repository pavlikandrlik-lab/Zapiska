using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RecordProposalDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RecordProposalDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SubmitCreateRecordProposal_ShouldPersistPendingProposal_WithoutOperationalRecord()
    {
        var db = await _fixture.CreateDatabaseAsync("record_proposal_create_submit");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ProposalCreateLead");
        var approverId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ProposalCreateManager");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RCPROP1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RCPROP1_SYS", proposerId);

        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, approverId, ProjectRoleCodes.ProjectManager);

        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);
        var editor = store.BuildCreateRecordProposalEditor(projectId, proposer);
        var command = BuildCreateProposalCommand(editor, projectId, approverId);

        store.SubmitCreateRecordProposal(command, proposer);

        var proposals = await dbContext.ZaznamNavrhy.AsNoTracking().ToListAsync();
        proposals.Should().ContainSingle();
        proposals[0].ProjektId.Should().Be(projectId);
        proposals[0].ZaznamId.Should().BeNull();
        proposals[0].SubsystemId.Should().Be(subsystemId);
        proposals[0].TypNavrhu.Should().Be(RecordProposalTypeCodes.CreateRecord);
        proposals[0].Stav.Should().Be(RecordProposalStateCodes.Pending);
        proposals[0].CreatedByOsobaId.Should().Be(proposerId);
        var recordCount = await dbContext.ProjektoveZaznamy.CountAsync();
        recordCount.Should().Be(0);
    }

    [Fact]
    public async Task ApproveCreateProposal_ShouldCreateOperationalRecord_AndMarkProposalApproved()
    {
        var db = await _fixture.CreateDatabaseAsync("record_proposal_create_approve");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ProposalApproveLead");
        var approverId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ProposalApproveManager");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RCPROP2");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RCPROP2_SYS", proposerId);

        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, approverId, ProjectRoleCodes.ProjectManager);

        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);
        var approver = IntegrationTestHelper.BuildUser(
            approverId,
            grants: [IntegrationTestHelper.AllowProjectPermission(PermissionKeys.RecordsEdit, projectId)]);
        var editor = store.BuildCreateRecordProposalEditor(projectId, proposer);
        var command = BuildCreateProposalCommand(editor, projectId, approverId, name: "Schvalovaný návrh");

        store.SubmitCreateRecordProposal(command, proposer);

        var proposalId = await dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .Select(x => x.Id)
            .SingleAsync();

        var approvedRecordId = store.ApproveProposal(new ProposalDecisionCommand
        {
            ProjektId = projectId,
            ProposalId = proposalId
        }, approver);

        approvedRecordId.Should().HaveValue();

        var proposal = await dbContext.ZaznamNavrhy.AsNoTracking().SingleAsync(x => x.Id == proposalId);
        proposal.Stav.Should().Be(RecordProposalStateCodes.Approved);
        proposal.DecidedByOsobaId.Should().Be(approverId);
        proposal.ApprovedRecordId.Should().Be(approvedRecordId);

        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == approvedRecordId!.Value);
        record.ProjektId.Should().Be(projectId);
        record.SubsystemId.Should().Be(subsystemId);
        record.Nazev.Should().Be("Schvalovaný návrh");
        record.VlastnikId.Should().Be(approverId);
    }

    [Fact]
    public async Task SaveRecord_WithPendingScheduleProposal_ShouldPreserveDeadlineAndPlannedValues()
    {
        var db = await _fixture.CreateDatabaseAsync("record_proposal_schedule_lock");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ProposalScheduleLead");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ProposalScheduleManager");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RCPROP3");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RCPROP3_SYS", proposerId);

        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);

        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "Lock");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);
        var editor = store.BuildZaznamEdit(recordId);
        var firstStep = editor.HarmonogramBlok.Kroky.First(x => x.TrvaniTypId > 0 && x.ZpozdeniTypId > 0);
        var originalDeadline = editor.TerminUkonceni!.Value.Date;

        dbContext.ZaznamHarmonogramHodnoty.AddRange(
            new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = recordId,
                TypId = firstStep.TrvaniTypId,
                HodnotaInt = 5,
                UpdatedAt = DateTime.UtcNow
            },
            new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = recordId,
                TypId = firstStep.ZpozdeniTypId,
                HodnotaInt = 1,
                UpdatedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var scheduleProposalEditor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var scheduleProposalCommand = BuildEditCommand(scheduleProposalEditor);
        scheduleProposalCommand.TerminUkonceni = originalDeadline.AddDays(10);
        scheduleProposalCommand.HarmonogramHodnoty =
        [
            new SaveRecordHarmonogramValueCommand { TypId = firstStep.TrvaniTypId, Hodnota = 9 },
            new SaveRecordHarmonogramValueCommand { TypId = firstStep.ZpozdeniTypId, Hodnota = 3 }
        ];

        store.SubmitScheduleProposal(scheduleProposalCommand, proposer);

        var editorWithLock = store.BuildZaznamEdit(recordId);
        editorWithLock.HasPendingScheduleProposalLock.Should().BeTrue();

        var manager = IntegrationTestHelper.BuildUser(
            managerId,
            grants: [IntegrationTestHelper.AllowProjectPermission(PermissionKeys.RecordsEdit, projectId)]);
        var saveCommand = BuildEditCommand(editorWithLock);
        saveCommand.Nazev = "Lock Record Updated";
        saveCommand.TerminUkonceni = originalDeadline.AddDays(20);
        saveCommand.HarmonogramHodnoty =
        [
            new SaveRecordHarmonogramValueCommand { TypId = firstStep.TrvaniTypId, Hodnota = 12 },
            new SaveRecordHarmonogramValueCommand { TypId = firstStep.ZpozdeniTypId, Hodnota = 4 }
        ];

        store.SaveRecord(saveCommand, manager);

        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var values = await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => x.ZaznamId == recordId && (x.TypId == firstStep.TrvaniTypId || x.TypId == firstStep.ZpozdeniTypId))
            .ToDictionaryAsync(x => x.TypId, x => x.HodnotaInt);

        record.Nazev.Should().Be("Lock Record Updated");
        record.DatumUkonceni.Date.Should().Be(originalDeadline);
        values[firstStep.TrvaniTypId].Should().Be(5);
        values[firstStep.ZpozdeniTypId].Should().Be(4);
    }

    private static SaveRecordCommand BuildCreateProposalCommand(ZaznamEditViewModel editor, int projectId, int ownerId, string name = "Nový návrh")
    {
        var subsystem = editor.Subsystemy
            .Select(option => string.IsNullOrWhiteSpace(option.Kod) ? option.Nazev : option.Kod)
            .First();

        return new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = editor.KategorieZaznamu.First(),
            TypUkolu = editor.TypyUkolu.FirstOrDefault(),
            Stav = editor.StavyUkolu.First(),
            Nazev = name,
            Cil = "Cíl návrhu",
            Popis = "<p>Popis návrhu</p>",
            VlastnikId = ownerId,
            DatumZalozeni = new DateTime(2026, 4, 1),
            TerminUkonceni = new DateTime(2026, 4, 30),
            Subsystem = subsystem,
            VybraniSpolupracovniciIds = [],
            ExterniVazby = [],
            HarmonogramHodnoty = []
        };
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
