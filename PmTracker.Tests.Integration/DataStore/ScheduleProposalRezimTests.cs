using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Task 5 (2026-10-12): přepínač „Automatické vyplňování harmonogramu" se ukládá do návrhu
/// změny termínu a harmonogramu (SchedulePlanProposalPayload.HarmonogramRezim) a po schválení
/// se opravdu provede na auto-eligible krocích (1/3/4/6/7/10) — stejné pravidlo jako
/// RecordService.SaveRecord. Starší návrhy (payload bez pole) se chovají jako dřív (Review
/// Focus 3). Návrh „Auto" nesoucí zastaralá ruční data pro auto-eligible kroky se po schválení
/// neuplatní (Review Focus 5).
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ScheduleProposalRezimTests
{
    private readonly SqlIntegrationFixture _fixture;
    public ScheduleProposalRezimTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task NavrhAutoNaRucne_PoSchvaleni_AutoEligibleKrokyRucne_RucniDatumUlozeno()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_auto_to_manual");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead1");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager1");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM1_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimAutoToManual");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        // Záznam bez krok řádků = Auto (HarmonogramAutoFillRule.IsAutoFill([]) == true).
        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var original = editor.TerminUkonceni!.Value.Date;
        var manualDatum = original.AddDays(-10);
        var command = BuildEditCommand(editor);
        command.HarmonogramRezim = "Manual";
        command.ManualActualKroky =
        [
            new ManualActualKrokDto { Poradi = 3, AbsolutniDatum = DateOnly.FromDateTime(manualDatum) }
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

        var kroky = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .ToListAsync();
        foreach (var poradi in new byte[] { 1, 4, 6, 7, 10 })
        {
            kroky.Single(k => k.Poradi == poradi).SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual,
                $"krok {poradi} je auto-eligible — přepínač Manual ho musí přepnout");
        }
        var krok3 = kroky.Single(k => k.Poradi == 3);
        krok3.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
        krok3.SkutecnostDatum.Should().Be(manualDatum);
    }

    [Fact]
    public async Task NavrhRucneNaAuto_PoSchvaleni_AutoEligibleKrokyAuto_RucniDataAutoKrokuNeuplatni()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_manual_to_auto");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead2");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager2");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM2");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM2_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimManualToAuto");

        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var skutecnostA = record.DatumUkonceni.AddDays(-20);
        foreach (var poradi in new byte[] { 1, 3, 4, 6, 7, 10 })
        {
            dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = poradi,
                SkutecnostDatum = poradi == 3 ? skutecnostA : null,
                SkutecnostRezim = (byte)SkutecnostRezimEnum.Manual,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Manual,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);
        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var skutecnostB = record.DatumUkonceni.AddDays(-5);
        var command = BuildEditCommand(editor);
        command.HarmonogramRezim = "Auto";
        command.ManualActualKroky =
        [
            new ManualActualKrokDto { Poradi = 3, AbsolutniDatum = DateOnly.FromDateTime(skutecnostB) }
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

        var kroky = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .ToListAsync();
        foreach (var poradi in new byte[] { 1, 3, 4, 6, 7, 10 })
        {
            kroky.Single(k => k.Poradi == poradi).SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Auto,
                $"krok {poradi} je auto-eligible — přepínač Auto ho musí přepnout");
        }
        var krok3 = kroky.Single(k => k.Poradi == 3);
        krok3.SkutecnostDatum.Should().Be(skutecnostA, "ruční datum B z návrhu se v Auto rezimu neuplatní");
    }

    [Fact]
    public async Task StaryNavrhBezRezimu_SchvaleniChovaJakoDriv()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_legacy");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead3");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager3");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM3");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM3_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimLegacy");

        // Krok 1 je pre-existující v Manual — payload bez HarmonogramRezim nesmí tento stav
        // přepsat (Review Focus 3: starší návrh se chová jako dřív = beze změny režimu).
        dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
        {
            ZaznamId = recordId,
            Poradi = 1,
            SkutecnostRezim = (byte)SkutecnostRezimEnum.Manual,
            SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Manual,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var manualDatum = DateTime.Today.AddDays(10);

        // Payload ve starém formátu — bez HarmonogramRezim property v JSON (simuluje návrh
        // persistovaný dřív, než pole existovalo — Task 5).
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
                    new { Poradi = 3, AbsolutniDatum = DateOnly.FromDateTime(manualDatum), PreferredZdroj = (string?)null }
                }
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
        store.ApproveProposal(new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposalId }, manager);

        var kroky = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .ToListAsync();
        var krok3 = kroky.Single(k => k.Poradi == 3);
        krok3.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
        krok3.SkutecnostDatum.Should().Be(manualDatum);

        var krok1 = kroky.Single(k => k.Poradi == 1);
        krok1.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual, "beze změny — payload neobsahuje HarmonogramRezim");
    }

    [Fact]
    public async Task NavrhJenSeZmenouRezimu_LzeOdeslat()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_only_change");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead4");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager4");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM4");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM4_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimOnlyChange");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        // Záznam bez krok řádků = Auto. Command beze změny termínu/plánu (BuildEditCommand
        // posílá HarmonogramHodnoty = []) — jen přepínač na Manual.
        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var command = BuildEditCommand(editor);
        command.HarmonogramRezim = "Manual";

        var act = () => store.SubmitScheduleProposal(command, proposer);
        act.Should().NotThrow("jen změna přepínače je validní důvod pro odeslání návrhu");

        var proposalRow = await dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ZaznamId == recordId && x.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange)
            .Select(x => x.PayloadJson)
            .SingleAsync();
        var payload = JsonSerializer.Deserialize<RecordProposalPayload>(proposalRow)!;
        payload.SchedulePlan!.ChangesHarmonogramRezim.Should().BeTrue();
        payload.SchedulePlan!.HarmonogramRezim.Should().Be("Manual");
    }

    /// <summary>
    /// Task 6 (2026-10-12): schvalovatel na detailu návrhu vidí stav přepínače z NÁVRHU, ne ze
    /// záznamu — a je upozorněn, že návrh režim mění (ProposalChangesHarmonogramRezim +
    /// ProposalRezimChangeMessage).
    /// </summary>
    [Fact]
    public async Task DetailNavrhu_UkazeRezimZNavrhu_AOznaciZmenu()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_detail_changed");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead5");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager5");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM5");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM5_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimDetailChanged");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        // Záznam bez krok řádků = Auto; návrh přepíná na Manual.
        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var command = BuildEditCommand(editor);
        command.HarmonogramRezim = "Manual";
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

        detail.HarmonogramAutoFillSwitchOn.Should().BeFalse("schvalovatel vidí stav z návrhu, ne ze záznamu");
        detail.ProposalChangesHarmonogramRezim.Should().BeTrue();
        detail.ProposalRezimChangeMessage.Should().Contain("ruční");
    }

    [Fact]
    public async Task DetailNavrhu_StejnyRezim_BezOznaceni()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_detail_unchanged");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead6");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager6");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM6");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM6_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimDetailUnchanged");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        // Záznam bez krok řádků = Auto; návrh nese stejné "Auto" — žádná reálná změna.
        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var original = editor.TerminUkonceni!.Value.Date;
        var command = BuildEditCommand(editor);
        command.TerminUkonceni = original.AddDays(14);
        command.HarmonogramRezim = "Auto";
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

        detail.HarmonogramAutoFillSwitchOn.Should().BeTrue();
        detail.ProposalChangesHarmonogramRezim.Should().BeFalse();
        detail.ProposalRezimChangeMessage.Should().BeNull();
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
