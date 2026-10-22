using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ServiceDesk;

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
        command.HarmonogramHodnoty =
        [
            new SaveRecordHarmonogramValueCommand { Poradi = 3, SkutecnostDatum = manualDatum }
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
        command.HarmonogramHodnoty =
        [
            new SaveRecordHarmonogramValueCommand { Poradi = 3, SkutecnostDatum = skutecnostB, SkutecnostDatumPuvodni = skutecnostA }
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
        krok3.SkutecnostDatum.Should().BeNull(
            "ruční datum B z návrhu se v Auto režimu neuplatní a ruční datum A se přepnutím na automatiku smaže (R2)");
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

    /// <summary>
    /// Review fix M3 (2026-10-12): spec known-issues §Poznámky k řešení — infobar se má zobrazit
    /// i u starších čekajících návrhů (payload bez HarmonogramRezim/ChangesHarmonogramRezim), které
    /// nesou ruční data (ManualActualKroky) pro krok, jenž je na ZÁZNAMU automatický. Switch se
    /// nesmí zvýraznit (ProposalChangesHarmonogramRezim zůstává false — žádný ÚMYSL měnit přepínač).
    /// </summary>
    [Fact]
    public async Task StaryNavrhRucniDataNaAutoKroku_DetailUkazeInfobarBezPodbarveniPrepinace()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_legacy_manual_on_auto");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead12");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager12");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM12");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM12_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        // Záznam bez krok řádků = všechny kroky Auto (včetně kroku 3).
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimLegacyManualOnAuto");

        var manualDatum = DateTime.Today.AddDays(10);

        // Payload ve starém formátu — bez HarmonogramRezim/ChangesHarmonogramRezim property v JSON
        // (simuluje návrh persistovaný dřív, než pole existovalo — Task 5). Krok 3 je auto-eligible
        // a na záznamu Auto → po schválení by se tiše přepnul na Manual beze zvýraznění přepínače.
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
        var detail = store.BuildProposalDetail(projectId, proposalId, manager);

        detail.ProposalChangesHarmonogramRezim.Should().BeFalse("switch se nesmí zvýraznit — návrh nenese ÚMYSL měnit přepínač");
        detail.ProposalRezimChangeMessage.Should().NotBeNullOrEmpty();
        detail.ProposalRezimChangeMessage.Should().Contain("3");
        detail.ProposalRezimChangeMessage.Should().Contain("vyplňované automaticky");
    }

    /// <summary>
    /// Review fix M3 (2026-10-12): doplněk k testu výše — ruční data jen pro čistě manuální kroky
    /// (2/5/8/9, které na záznamu nikdy nejsou auto-eligible) nesmí infobar vyvolat.
    /// </summary>
    [Fact]
    public async Task StaryNavrhRucniDataJenNaManualKrocich_DetailBezInfobaru()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_legacy_manual_only");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead13");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager13");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM13");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM13_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimLegacyManualOnly");

        var manualDatum2 = DateTime.Today.AddDays(10);
        var manualDatum5 = DateTime.Today.AddDays(12);

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
                    new { Poradi = 2, AbsolutniDatum = DateOnly.FromDateTime(manualDatum2), PreferredZdroj = (string?)null },
                    new { Poradi = 5, AbsolutniDatum = DateOnly.FromDateTime(manualDatum5), PreferredZdroj = (string?)null }
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
        var detail = store.BuildProposalDetail(projectId, proposalId, manager);

        detail.ProposalChangesHarmonogramRezim.Should().BeFalse();
        detail.ProposalRezimChangeMessage.Should().BeNullOrEmpty("ruční data jen pro kroky 2/5/8/9 — ty jsou na záznamu vždy manuální, žádné upozornění");
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

    /// <summary>
    /// Fix round 1 (2026-10-12, Important finding): editor VŽDY posílá hidden HarmonogramRezim
    /// (i když se nezměnil) — ChangesHarmonogramRezim je jediný signál, že návrh rezim opravdu
    /// MĚNÍ. Návrh odeslaný beze změny (HarmonogramRezim == aktuální stav záznamu) nesmí po
    /// schválení přepsat rezim, který se mezitím na záznamu změnil přímou editací.
    /// </summary>
    [Fact]
    public async Task NavrhBezZmenyRezimu_ZaznamMezitimRucne_SchvaleniRezimNezmeni()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_stale_overwrite");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead7");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager7");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM7");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM7_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimStaleOverwrite");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        // Záznam bez krok řádků = Auto. Editor VŽDY posílá HarmonogramRezim — tady "Auto", stejné
        // jako aktuální stav → ChangesHarmonogramRezim bude false. Návrh jen posouvá termín.
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

        // Mezitím (po odeslání návrhu) se záznam přímou editací přepne na Manual — pending lock
        // ChangesHarmonogramRezim=false nechránil harmonogram, takže to je legitimní přímá editace.
        foreach (var poradi in new byte[] { 1, 3, 4, 6, 7, 10 })
        {
            dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = poradi,
                SkutecnostRezim = (byte)SkutecnostRezimEnum.Manual,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Manual,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

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
            kroky.Single(k => k.Poradi == poradi).SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual,
                $"návrh nenesl ÚMYSL změnit rezim (ChangesHarmonogramRezim=false) — mezitímní přímá editace na Manual se nesmí přepsat");
        }
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        record.DatumUkonceni.Date.Should().Be(original.AddDays(14), "posun termínu se naopak uplatnit měl");
    }

    /// <summary>
    /// Fix round 1 (2026-10-12): návrh beze změny rezimu nesmí založit prázdné krok řádky pro
    /// auto-eligible kroky jen proto, že editor hidden pole vždy posílá (viz předchozí test).
    /// </summary>
    [Fact]
    public async Task NavrhBezZmenyRezimu_ZaznamBezKroku_SchvaleniNezalozPrazdneRadky()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_no_empty_rows");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead8");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager8");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM8");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM8_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimNoEmptyRows");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var original = editor.TerminUkonceni!.Value.Date;
        var command = BuildEditCommand(editor);
        command.TerminUkonceni = original.AddDays(14);
        command.HarmonogramRezim = "Auto"; // stejné jako aktuální stav bez krok řádků → beze změny
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

        var krokCount = await dbContext.ZaznamHarmonogramKroky.AsNoTracking().CountAsync(x => x.ZaznamId == recordId);
        krokCount.Should().Be(0, "beze změny rezimu se nesmí založit prázdné krok řádky pro auto-eligible kroky");
    }

    /// <summary>
    /// Fix round 1 (2026-10-12): detail návrhu beze změny rezimu musí ukázat AKTUÁLNÍ stav
    /// záznamu (ne stav z návrhu) a žádný infobar, i když se záznam mezitím přímou editací
    /// přepnul na Manual.
    /// </summary>
    [Fact]
    public async Task DetailNavrhu_RezimBezZmeny_ZaznamMezitimZmenen_BezInfobaru()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_detail_stale");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead9");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager9");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM9");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM9_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimDetailStale");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

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

        // Mezitím (po odeslání návrhu) se záznam přímou editací přepne na Manual.
        foreach (var poradi in new byte[] { 1, 3, 4, 6, 7, 10 })
        {
            dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = poradi,
                SkutecnostRezim = (byte)SkutecnostRezimEnum.Manual,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Manual,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

        var manager = IntegrationTestHelper.BuildUser(
            managerId,
            visibleProjectIds: [projectId],
            grants: [IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsAccept, projectId)]);
        var detail = store.BuildProposalDetail(projectId, proposalId, manager);

        detail.ProposalChangesHarmonogramRezim.Should().BeFalse();
        detail.ProposalRezimChangeMessage.Should().BeNull();
        detail.HarmonogramAutoFillSwitchOn.Should().BeFalse("beze změny rezimu v návrhu se zobrazí AKTUÁLNÍ stav záznamu (Manual), ne stav z návrhu");
    }

    /// <summary>
    /// Spec 2026-10-14 R2: schválení přepnutí na automatiku smaže data, která nedodal automat,
    /// a automat krok hned doplní z vyjádření, které už je na záznamu navázané (harvest sám
    /// automat spouští jen při NOVÉM vyjádření) — stejně jako uložení editoru.
    /// </summary>
    [Fact]
    public async Task NavrhRucneNaAuto_PoSchvaleni_AutomatHnedDoplniKrokZNavazanehoVyjadreni()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_auto_sync");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, configureServices: services =>
        {
            services.AddScoped<PmTracker.ServiceDesk.Contracts.IVyjadreniQueryService>(_ => new PevnyTypTiketuVyjadreniQueryService("PNF"));
            services.AddSingleton<IHarvestScheduler>(new RecordingHarvestScheduler());
        });

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead12");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager12");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM12");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM12_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimAutoSync");

        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var rucni = record.DatumUkonceni.AddDays(-20);
        var zVyjadreni = record.DatumUkonceni.AddDays(-10);
        await VyjadreniVazbaSeed.SeedPnfVazbaSVyjadrenimAsync(dbContext, recordId, poradi: 6, datum: zVyjadreni);
        foreach (var poradi in new byte[] { 1, 3, 4, 6, 7, 10 })
        {
            dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = poradi,
                SkutecnostDatum = poradi == 6 ? rucni : null,
                SkutecnostRezim = (byte)SkutecnostRezimEnum.Manual,
                SkutecnostZdroj = poradi == 6 ? (byte)SkutecnostZdrojEnum.Manual : (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);
        var command = BuildEditCommand(store.BuildScheduleProposalEditor(projectId, recordId, proposer));
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
        store.ApproveProposal(new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposalId }, manager);

        var krok6 = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .SingleAsync(x => x.ZaznamId == recordId && x.Poradi == 6);
        krok6.SkutecnostDatum.Should().Be(zVyjadreni, "ruční datum se smaže a automat ho hned nahradí datem z vyjádření");
        krok6.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
    }

    /// <summary>
    /// Review fix M2 (2026-10-12): schválení přepnutí na Auto naplánuje harvest nad rámec
    /// SaveRecord (viz opravený komentář v DecisionCommands.cs) — refill ze ServiceDesku dřív,
    /// než doběhne periodický sync. Dřív netestováno (ledger: "non-enqueue not asserted", ale
    /// enqueue samo o sobě také chybělo).
    /// </summary>
    [Fact]
    public async Task NavrhRucneNaAuto_PoSchvaleni_NaplanujeJedenHarvestNaZaznam()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_harvest_enqueue");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var harvestScheduler = new RecordingHarvestScheduler();
        var store = IntegrationTestHelper.CreateDataStore(dbContext,
            configureServices: services => services.AddSingleton<IHarvestScheduler>(harvestScheduler));

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead10");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager10");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM10");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM10_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimHarvestEnqueue");

        // Záznam je Manual (krok řádky existují), návrh přepne na Auto.
        foreach (var poradi in new byte[] { 1, 3, 4, 6, 7, 10 })
        {
            dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = poradi,
                SkutecnostRezim = (byte)SkutecnostRezimEnum.Manual,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Manual,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);
        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var command = BuildEditCommand(editor);
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
        store.ApproveProposal(new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposalId }, manager);

        harvestScheduler.RecordHarvestCalls.Should().ContainSingle().Which.Should().Be(recordId);
        harvestScheduler.LinkHarvestCalls.Should().BeEmpty();
    }

    /// <summary>
    /// Review fix M2 (2026-10-12): doplňuje ledger "non-enqueue not asserted" — návrh beze
    /// změny rezimu (ChangesHarmonogramRezim=false) nenaplánuje harvest, i když se mezitím
    /// záznam přímou editací přepnul na Manual (stejný scénář jako
    /// <see cref="NavrhBezZmenyRezimu_ZaznamMezitimRucne_SchvaleniRezimNezmeni"/> výše).
    /// </summary>
    [Fact]
    public async Task NavrhBezZmenyRezimu_ZaznamMezitimRucne_SchvaleniNenaplanujeHarvest()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_rezim_harvest_no_enqueue");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var harvestScheduler = new RecordingHarvestScheduler();
        var store = IntegrationTestHelper.CreateDataStore(dbContext,
            configureServices: services => services.AddSingleton<IHarvestScheduler>(harvestScheduler));

        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimLead11");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RezimManager11");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REZIM11");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REZIM11_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "RezimHarvestNoEnqueue");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        // Záznam bez krok řádků = Auto. Editor VŽDY posílá HarmonogramRezim — tady "Auto", stejné
        // jako aktuální stav → ChangesHarmonogramRezim bude false. Návrh jen posouvá termín.
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

        // Mezitím se záznam přímou editací přepne na Manual — pending lock ChangesHarmonogramRezim=false
        // nechránil harmonogram (fix-round ruling), schválení přesto nesmí poslat harvest navíc.
        foreach (var poradi in new byte[] { 1, 3, 4, 6, 7, 10 })
        {
            dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = poradi,
                SkutecnostRezim = (byte)SkutecnostRezimEnum.Manual,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Manual,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

        var manager = IntegrationTestHelper.BuildUser(
            managerId,
            visibleProjectIds: [projectId],
            grants: [IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsAccept, projectId)]);
        store.ApproveProposal(new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposalId }, manager);

        harvestScheduler.RecordHarvestCalls.Should().BeEmpty("návrh nenesl ÚMYSL změnit rezim — schválení nesmí naplánovat harvest navíc");
        harvestScheduler.LinkHarvestCalls.Should().BeEmpty();
    }

    private static SaveRecordCommand BuildEditCommand(ZaznamEditViewModel editor)
        => IntegrationTestHelper.BuildScheduleEditCommand(editor);
}
