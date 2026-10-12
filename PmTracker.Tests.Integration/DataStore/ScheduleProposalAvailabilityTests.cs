using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ScheduleProposalAvailabilityTests
{
    private readonly SqlIntegrationFixture _fixture;
    public ScheduleProposalAvailabilityTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Dostupnost_BezCekajicihoNavrhu_LzeZalozit_SCekajicimNe()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_availability");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AvailLead");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AvailManager");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "AVAIL1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "AVAIL1_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "Avail");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        var volno = store.GetScheduleProposalAvailability(projectId, recordId, proposer);
        volno.CanCreate.Should().BeTrue();
        volno.Reason.Should().Be(ScheduleProposalBlockReason.None);

        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var command = BuildEditCommand(editor);
        command.TerminUkonceni = editor.TerminUkonceni!.Value.AddDays(7);
        store.SubmitScheduleProposal(command, proposer);
        var proposalId = await dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ZaznamId == recordId).Select(x => x.Id).SingleAsync();

        var obsazeno = store.GetScheduleProposalAvailability(projectId, recordId, proposer);
        obsazeno.CanCreate.Should().BeFalse();
        obsazeno.Reason.Should().Be(ScheduleProposalBlockReason.PendingProposal);
        obsazeno.PendingProposalId.Should().Be(proposalId);
        obsazeno.Message.Should().Be(ScheduleProposalAvailability.PendingMessage);
    }

    [Fact]
    public async Task Dostupnost_BezOpravneniKSubsystemu_NelzeZalozit()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_availability_noperm");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AvailLead2");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AvailManager2");
        var cizId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AvailCizi");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "AVAIL2");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "AVAIL2_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "AvailNoPerm");

        // Osoba bez role v subsystému záznamu a bez proposals.edit.any.
        var cizi = IntegrationTestHelper.BuildUser(cizId, visibleProjectIds: [projectId]);

        var result = store.GetScheduleProposalAvailability(projectId, recordId, cizi);
        result.CanCreate.Should().BeFalse();
        result.Reason.Should().Be(ScheduleProposalBlockReason.NoPermission);
        result.Message.Should().Be(ScheduleProposalAvailability.NoPermissionMessage);
        result.PendingProposalId.Should().BeNull();
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
