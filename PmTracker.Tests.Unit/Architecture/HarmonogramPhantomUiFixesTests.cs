using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Phase 13 (Plán Harmonogram refactor 2026-05-01) — architecture testy pro
/// phantom UI fixes a klíčové invariants. Cíl: zajistit, že refactor zůstane
/// v platnosti i po budoucích změnách (= regrese by tyto testy odhalily).
/// </summary>
public sealed class HarmonogramPhantomUiFixesTests
{
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relPath)
        => File.ReadAllText(Path.Combine(LocateRepoRoot(), relPath));

    [Fact]
    public void SelectCandidateJs_Existuje()
    {
        var path = Path.Combine(LocateRepoRoot(), "PmTracker.Web/wwwroot/js/modules/schedule-feature-c/select-candidate.js");
        File.Exists(path).Should().BeTrue("DESIGN-9-A — select-candidate.js musí existovat (phantom UI bug 3 fix).");
    }

    [Fact]
    public void Bootstrap_ImportujeSelectCandidate()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        src.Should().Contain("schedule-feature-c/select-candidate",
            "bootstrap.js musí importovat select-candidate.js (memory: project_bundle_sync — side-effect imports povinné).");
    }

    [Fact]
    public void Bootstrap_ImportujePreviewSync()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        src.Should().Contain("schedule-feature-c/preview-sync",
            "bootstrap.js musí importovat preview-sync.js (Phase 12 DESIGN-9-C).");
    }

    [Fact]
    public void PreviewSyncJs_Existuje()
    {
        var path = Path.Combine(LocateRepoRoot(), "PmTracker.Web/wwwroot/js/modules/schedule-feature-c/preview-sync.js");
        File.Exists(path).Should().BeTrue("DESIGN-9-C — preview-sync.js pro pre-fetch staging.");
    }

    [Fact]
    public void RecordService_SaveRecord_PersistujeManualActualKroky()
    {
        var src = Read("PmTracker.Web/Services/RecordService.SaveRecord.cs");
        src.Should().Contain("ApplyManualActualKrokyAsync",
            "DESIGN-6-A — phantom UI bug 1 fix: ManualActualKroky persistence v save flow.");
    }

    [Fact]
    public void HarmonogramController_ToggleRezim_MaCreateIfMissing()
    {
        var src = Read("PmTracker.Web/Controllers/HarmonogramController.cs");
        src.Should().MatchRegex(@"ToggleRezimRequest\(\s*int HodnotaId,\s*SkutecnostRezimEnum Rezim,\s*int\? ZaznamId",
            "DESIGN-7-B — ToggleRezim musí podporovat create-if-missing přes ZaznamId+KrokPoradi (phantom UI bug 2 fix).");
    }

    [Fact]
    public void HarmonogramController_ToggleRezim_MaPendingLockPreCheck()
    {
        var src = Read("PmTracker.Web/Controllers/HarmonogramController.cs");
        src.Should().Contain("_pendingLockEvaluator",
            "DESIGN-7-B — ToggleRezim musí pre-checkovat pending lock před Manual→Auto.");
    }

    [Fact]
    public void HarmonogramController_PreviewSyncEndpoint_Existuje()
    {
        var src = Read("PmTracker.Web/Controllers/HarmonogramController.cs");
        src.Should().Contain("PreviewSyncRequest",
            "DESIGN-9-C — POST /Harmonogram/PreviewSync staging endpoint.");
    }

    [Fact]
    public void ManualProposalFieldValidator_ValidateAutoStepNotInProposal_Existuje()
    {
        var src = Read("PmTracker.Web/Services/Records/ManualProposalFieldValidator.cs");
        src.Should().Contain("ValidateAutoStepNotInProposal",
            "DESIGN-5-A/7-A — auto step rejection validátor v návrhu.");
    }

    [Fact]
    public void ScheduleEditorPermissionSet_ObsahujeNoveFlagy()
    {
        var src = Read("PmTracker.Web/Services/Schedules/ScheduleEditorPermissionSet.cs");
        src.Should().Contain("CanEditScheduleDirect",
            "DESIGN-9-B sjednocení permission flagů — records.schedule.edit gate.");
        src.Should().Contain("CanProposeSchedule",
            "DESIGN-9-B — proposals.schedule.create gate.");
        src.Should().Contain("CanEditManualActual",
            "DESIGN-6-C — composite flag pro manuální cell input.");
    }

    [Fact]
    public void HarmonogramSyncPlan_DTO_Existuje()
    {
        var path = Path.Combine(LocateRepoRoot(), "PmTracker.Web/Services/Schedules/HarmonogramSyncPlan.cs");
        File.Exists(path).Should().BeTrue("DESIGN-4-A — HarmonogramSyncPlan DTO pro Compute+Apply split.");
    }

    [Fact]
    public void HarmonogramSyncService_ImplementujeComputePlan()
    {
        var src = Read("PmTracker.Web/Services/Schedules/HarmonogramSkutecnostSyncService.cs");
        src.Should().Contain("ComputePlanAsync",
            "DESIGN-4-A — pure read-only ComputePlanAsync.");
        src.Should().Contain("ApplyPlanAsync",
            "DESIGN-4-A — write ApplyPlanAsync s optimistic concurrency.");
    }

    [Fact]
    public void ZaznamNavrhEntity_ObsahujeSupersededByProposalId()
    {
        var src = Read("PmTracker.Web/Models/Entities/PmTrackerEntities.cs");
        src.Should().Contain("SupersededByProposalId",
            "DESIGN-7-D — auto-supersede chain.");
    }

    [Fact]
    public void RecordProposalStateCodes_ObsahujeSuperseded()
    {
        var src = Read("PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs");
        src.Should().Contain("Superseded = \"SUPERSEDED\"",
            "DESIGN-7-D — Superseded state code pro auto-supersede.");
    }

    [Fact]
    public void HodnotaInt_JeNullable()
    {
        var src = Read("PmTracker.Web/Models/Entities/PmTrackerEntities.cs");
        src.Should().Contain("public int? HodnotaInt",
            "DESIGN-10-A — nullable HodnotaInt (NULL = krok nenastal).");
    }

    [Fact]
    public void ScheduleBlock_NeFiltrujeZeroDuration()
    {
        var src = Read("PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml");
        src.Should().NotContain("Where(k => k.TrvaniDni > 0)",
            "DESIGN-9-D — žádné skrývání kroků (memory: project_harmonogram_visibility_rules).");
        src.Should().NotContain("Where(krok => krok.TrvaniDni > 0)",
            "DESIGN-9-D — compact rainbow nesmí filtrovat zero-duration.");
    }

    [Fact]
    public void DbMigrace_1_3_13_proposalSupersede_Existuje()
    {
        var path = Path.Combine(LocateRepoRoot(), "db_upgrade_1_3_13_proposal_supersede.sql");
        File.Exists(path).Should().BeTrue("Phase 1 migrace pro DESIGN-7-D.");
    }

    [Fact]
    public void DbMigrace_1_3_14_delayNullable_Existuje()
    {
        var path = Path.Combine(LocateRepoRoot(), "db_upgrade_1_3_14_delay_nullable.sql");
        File.Exists(path).Should().BeTrue("Phase 1.5 migrace pro DESIGN-10-A.");
    }
}
