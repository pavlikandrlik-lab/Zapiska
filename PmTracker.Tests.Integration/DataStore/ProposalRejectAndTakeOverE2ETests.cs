using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ProposalRejectAndTakeOverE2ETests
{
    private readonly SqlIntegrationFixture _fixture;

    public ProposalRejectAndTakeOverE2ETests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Převzetí návrhu do formuláře je rozhodnutí o návrhu: kromě proposals.takeover (endpoint)
    /// vyžaduje i proposals.accept (CanDecideProjectProposalAsync). Rozhodnutí uživatele
    /// 2026-11-11 (varianta B) — v seedu má převzetí každá role, která smí schválit.
    /// </summary>
    [Fact]
    public async Task RejectAndTakeOver_ThenSaveWithModifiedData_CreatesRecord()
    {
        var db = await _fixture.CreateDatabaseAsync("proposal_reject_takeover_e2e");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var (projectId, managerId, proposalId) = await SeedPendingCreateProposalAsync(dbContext, store, "TAKEOVER1");
        var manager = IntegrationTestHelper.BuildUser(
            managerId,
            grants:
            [
                IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsTakeover, projectId),
                IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsAccept, projectId),
                IntegrationTestHelper.AllowProjectPermission(PermissionKeys.RecordsCreate, projectId)
            ]);

        // --- 1. Zamítnout a převzít ---
        store.RejectAndTakeOverCreateProposal(
            new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposalId },
            manager);

        var rejectedProposal = await dbContext.ZaznamNavrhy.AsNoTracking().SingleAsync(x => x.Id == proposalId);
        rejectedProposal.Stav.Should().Be(RecordProposalStateCodes.Rejected, "návrh musí být zamítnut");
        rejectedProposal.DecidedByOsobaId.Should().Be(managerId);

        // --- 2. Načíst předvyplněný editor ---
        var prefilled = store.BuildPrefilledCreateEditorFromProposal(projectId, proposalId, manager);

        prefilled.IsCreate.Should().BeTrue("formulář musí být v create režimu");
        prefilled.Nazev.Should().Be("Původní návrh k převzetí", "název z návrhu převzat");
        prefilled.Cil.Should().Be("Původní cíl", "cíl z návrhu převzat");
        prefilled.TerminUkonceni.Should().Be(new DateTime(2026, 6, 30), "termín z návrhu převzat");

        // --- 3. Upravit data a uložit jako regulérní záznam ---
        var saveCommand = new SaveRecordCommand
        {
            ProjektId = prefilled.ProjektId,
            Kategorie = prefilled.Kategorie,
            TypUkolu = prefilled.TypUkolu,
            Stav = prefilled.Stav,
            Nazev = "Upravený záznam po převzetí",
            Cil = "Nový cíl manažera",
            Popis = prefilled.Popis,
            VlastnikId = managerId,
            DatumZalozeni = prefilled.DatumZalozeni,
            TerminUkonceni = new DateTime(2026, 7, 15),
            Subsystem = prefilled.Subsystemy.Select(s => string.IsNullOrWhiteSpace(s.Kod) ? s.Nazev : s.Kod).First(),
            VybraniSpolupracovniciIds = [],
            ExterniVazby = [],
            HarmonogramHodnoty = []
        };

        var recordId = store.SaveRecord(saveCommand, manager);

        // --- 4. Ověřit výsledný záznam ---
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        record.ProjektId.Should().Be(projectId);
        record.Nazev.Should().Be("Upravený záznam po převzetí");
        record.Cil.Should().Be("Nový cíl manažera");
        record.DatumUkonceni.Date.Should().Be(new DateTime(2026, 7, 15));
        record.VlastnikId.Should().Be(managerId);
    }

    [Fact]
    public async Task RejectAndTakeOver_BezPravaSchvalit_JeOdmitnuto_NavrhCekaDal()
    {
        var db = await _fixture.CreateDatabaseAsync("proposal_takeover_without_accept");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var (projectId, managerId, proposalId) = await SeedPendingCreateProposalAsync(dbContext, store, "TAKEOVER2");
        var bezSchvaleni = IntegrationTestHelper.BuildUser(
            managerId,
            grants:
            [
                IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsTakeover, projectId),
                IntegrationTestHelper.AllowProjectPermission(PermissionKeys.RecordsCreate, projectId)
            ]);

        var prevzeti = () => store.RejectAndTakeOverCreateProposal(
            new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposalId },
            bezSchvaleni);

        prevzeti.Should().Throw<InvalidOperationException>().WithMessage("*proposals.accept*");
        (await dbContext.ZaznamNavrhy.AsNoTracking().SingleAsync(x => x.Id == proposalId)).Stav
            .Should().Be(RecordProposalStateCodes.Pending, "odmítnuté převzetí nesmí návrh zamítnout");
    }

    /// <summary>Návrh založení záznamu od vedoucího subsystému, čekající na rozhodnutí PM.</summary>
    private static async Task<(int ProjectId, int ManagerId, int ProposalId)> SeedPendingCreateProposalAsync(
        PmTrackerDbContext dbContext, IntegrationTestDataStore store, string marker)
    {
        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Proposer");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Manager");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, marker);
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, $"{marker}_SYS", proposerId);

        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);

        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);
        var editor = store.BuildCreateRecordProposalEditor(projectId, proposer);
        store.SubmitCreateRecordProposal(new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = editor.KategorieZaznamu.First(),
            TypUkolu = editor.TypyUkolu.FirstOrDefault(),
            Stav = editor.StavyUkolu.First(),
            Nazev = "Původní návrh k převzetí",
            Cil = "Původní cíl",
            Popis = "<p>Původní popis</p>",
            VlastnikId = proposerId,
            DatumZalozeni = new DateTime(2026, 5, 1),
            TerminUkonceni = new DateTime(2026, 6, 30),
            Subsystem = editor.Subsystemy.Select(s => string.IsNullOrWhiteSpace(s.Kod) ? s.Nazev : s.Kod).First(),
            VybraniSpolupracovniciIds = [],
            ExterniVazby = [],
            HarmonogramHodnoty = []
        }, proposer);

        var proposalId = await dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .Select(x => x.Id)
            .SingleAsync();
        return (projectId, managerId, proposalId);
    }
}
