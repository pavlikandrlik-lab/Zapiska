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
        // FIX 2026-05-01 (round 2): metoda byla přejmenována Apply→Stage v post-review fix #3
        // (transakce semantics — caller ovládá SaveChanges, ne metoda samotná).
        var src = Read("PmTracker.Web/Services/RecordService.SaveRecord.cs");
        src.Should().Contain("StageManualActualKrokyAsync",
            "DESIGN-6-A + post-review fix #3 — phantom UI bug 1 fix: ManualActualKroky stage do change trackeru, caller commit + audit v outer transakci.");
        src.Should().Contain("command.ManualActualKroky",
            "Persistence flow musí číst command.ManualActualKroky z input commandu.");
        src.Should().Contain("SkutecnostZdrojEnum.Manual",
            "User-staged manual values musí dostat Zdroj=Manual.");
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

    [Fact]
    public void PmDateField_CustomElement_Existuje()
    {
        // FIX 2026-05-02: <pm-date-field> je native Custom Element (customElements.define),
        // sjednocený datumový vstup pro celou aplikaci (memory: feedback_web_component_means_custom_element).
        var path = Path.Combine(LocateRepoRoot(), "PmTracker.Web/wwwroot/js/components/pm-date-field.js");
        File.Exists(path).Should().BeTrue("pm-date-field.js Custom Element musí existovat.");
        var src = Read("PmTracker.Web/wwwroot/js/components/pm-date-field.js");
        src.Should().Contain("customElements.define(\"pm-date-field\"",
            "musí registrovat <pm-date-field> jako Custom Element.");
        src.Should().Contain("class PmDateFieldElement extends HTMLElement",
            "musí být regulérní HTMLElement subclass (ne Razor partial / JS modul).");
    }

    [Fact]
    public void Bootstrap_ImportujePmDateField()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        src.Should().Contain("components/pm-date-field",
            "bootstrap.js musí importovat pm-date-field.js side-effect (memory: project_bundle_sync).");
    }

    [Fact]
    public void AppDateField_Partial_RenderujePmDateFieldElement()
    {
        // _AppDateField partial je teď thin wrapper kolem Custom Elementu — všechny callsity
        // (základní údaje, harmonogram plán + skutečnost, NewMeetingModal) tím transitivně
        // používají <pm-date-field> bez nutnosti měnit jejich Razor.
        var src = Read("PmTracker.Web/Views/Shared/_AppDateField.cshtml");
        src.Should().Contain("<pm-date-field",
            "_AppDateField partial musí renderovat <pm-date-field> Custom Element.");
        src.Should().NotContain("data-app-date-field",
            "Razor partial nesmí duplikovat HTML strukturu Custom Elementu — markup je teď v JS.");
    }

    [Fact]
    public void ScheduleComposition_FiltrujeNullHodnotaInt_PredCastem()
    {
        // FIX 2026-05-01 (round 5 #2): cast Dictionary<int, int?> → IReadOnlyDictionary<int, int>
        // by selhal runtime InvalidCastException (kovariance generik nepodporuje).
        // Po DESIGN-10-A (Phase 1.5) je HodnotaInt nullable, takže ToDictionary value type
        // se musí filtrovat (.Where(.HasValue)) + použít !.Value PŘED castem.
        var src = Read("PmTracker.Web/Services/ProjectService.ScheduleComposition.cs");
        src.Should().Contain("HodnotaInt.HasValue",
            "ScheduleComposition musí filtrovat NULL HodnotaInt před castem na non-nullable IReadOnlyDictionary<int,int>.");
        src.Should().NotMatchRegex(@"item\s*=>\s*item\.HodnotaInt\s*\)\s*\)",
            "Bare `item => item.HodnotaInt` jako ToDictionary value bez .HasValue filtru způsobí runtime InvalidCastException.");
    }

    [Fact]
    public void ProjectDashboardService_FiltrujeNullHodnotaInt_PredCastem()
    {
        // FIX 2026-05-01 (round 6 #1): identický pattern jako ScheduleComposition (round 5 #2).
        // ProjectDashboardService.cs měl Dictionary<int, int?> → IReadOnlyDictionary<int, int>
        // cast → runtime InvalidCastException při dashboard load. Stejně musí filtrovat
        // .Where(.HasValue) + !.Value před castem.
        var src = Read("PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs");
        src.Should().Contain("HodnotaInt.HasValue",
            "ProjectDashboardService musí filtrovat NULL HodnotaInt před castem na non-nullable IReadOnlyDictionary<int,int>.");
        src.Should().NotMatchRegex(@"v\s*=>\s*v\.HodnotaInt\s*\)\s*\)",
            "Bare `v => v.HodnotaInt` jako ToDictionary value bez .HasValue filtru způsobí runtime InvalidCastException.");
    }

    [Fact]
    public void ScheduleBlock_DelayDateField_RespektujeNullOdchylka()
    {
        // FIX 2026-05-02: scenario D (auto krok + Zdroj=Manual / Create flow) renderuje DELAY date
        // field přes _AppDateField. Před fixem IsoValue=krok.SkutecneDatum.ToString(...) bez kontroly
        // OdchylkaDni.HasValue → calculator pro krok bez delay rowu vrátil SkutecneDatum=BaselineDatum
        // (= DatumZalozeni pro krok 1) → user viděl "datum založení" v prázdném políčku skutečnost.
        var src = Read("PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml");
        src.Should().Contain("OdchylkaDni.HasValue",
            "DELAY date field musí mít NULL guard pro krok bez záznamu (DESIGN-10-A NULL semantika).");
        src.Should().NotMatchRegex(@"IsoValue\s*=\s*krok\.SkutecneDatum\.ToString",
            "Bare krok.SkutecneDatum.ToString jako IsoValue bez null guardu zobrazí computed plan datum místo prázdné políčko.");
    }

    [Fact]
    public void HarmonogramController_SelectCandidate_RespektujePendingLock()
    {
        // FIX 2026-05-01 (round 5 #1): SelectCandidate musí volat IPendingScheduleProposalLockEvaluator
        // shodně s ToggleRezim. Auto-fill change během pending návrhu = bypass invariantu.
        var src = Read("PmTracker.Web/Controllers/HarmonogramController.cs");
        // Najdeme úsek SelectCandidate (od metody do konce před PreviewSyncRequest record)
        var selectStart = src.IndexOf("public async Task<IActionResult> SelectCandidate", StringComparison.Ordinal);
        var selectEnd = src.IndexOf("public sealed record PreviewSyncRequest", StringComparison.Ordinal);
        selectStart.Should().BeGreaterThan(0, "metoda SelectCandidate musí existovat.");
        selectEnd.Should().BeGreaterThan(selectStart);
        var section = src[selectStart..selectEnd];
        section.Should().Contain("_pendingLockEvaluator.EvaluateAsync",
            "round 5 #1 — SelectCandidate musí provolat pending lock evaluator pro audit-aware blocking.");
        section.Should().Contain("LockedManualKrokKeys",
            "round 5 #1 — kontrola krok keys v locked set.");
    }
}
