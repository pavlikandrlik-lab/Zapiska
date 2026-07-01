namespace PmTracker.Web.Models.ViewModels;

public sealed class ZaznamEditViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackLabel { get; set; }
    public int Id { get; set; }
    public int CisloZaznamu { get; set; }
    public required string CisloViditelne { get; set; }
    public int ProjektId { get; set; }
    public bool IsCreate { get; set; }
    public bool PouzivatIdentJednani { get; set; }
    public bool MaDostupneJednaniProCislo { get; set; }
    public bool MuzeDoplnitIdentifikatorJednani { get; set; }
    public int? JednaniIdProCislo { get; set; }
    public required IReadOnlyList<JednaniOptionViewModel> JednaniProCisloOptions { get; set; }
    public required string Nazev { get; set; }
    public required string Cil { get; set; }
    public required string Kategorie { get; set; }
    public required string Popis { get; set; }
    public string? TypUkolu { get; set; }
    public required string Stav { get; set; }
    public required IReadOnlyList<string> KategorieZaznamu { get; set; }
    public required IReadOnlyList<string> StavyUkolu { get; set; }
    public required IReadOnlyList<string> TypyUkolu { get; set; }
    public DateTime DatumZalozeni { get; set; }
    public DateTime? TerminUkonceni { get; set; }
    public required IReadOnlyList<SubsystemOptionViewModel> Subsystemy { get; set; }
    public required string Subsystem { get; set; }
    public required IReadOnlyList<LookupOptionViewModel> Vlastnici { get; set; }
    public int VlastnikId { get; set; }
    public bool JeUkolKategorie { get; set; }
    /// <summary>
    /// FIX 2026-05-03 — initial state pro master switch "Automatické vyplňování harmonogramu"
    /// v tab strip řádku. ON = všechny existující krok řádky v Auto rezimu (= classic
    /// auto-fill flow). OFF = aspoň jeden krok v Manual rezimu (= mix nebo všechny ručně).
    /// Pro nový záznam (IsCreate) defaultně true.
    /// </summary>
    public bool HarmonogramAutoFillSwitchOn { get; set; } = true;
    public HarmonogramBlockViewModel HarmonogramBlok { get; set; } = new();
    public required IReadOnlyList<SpolupracovnikOptionViewModel> DostupniVlastnici { get; set; }
    public required IReadOnlyList<SpolupracovnikOptionViewModel> DostupniSpolupracovnici { get; set; }
    public required IReadOnlyList<int> VybraniSpolupracovniciIds { get; set; }
    public required IReadOnlyList<ExterniOdkazEditViewModel> ExterniVazby { get; set; }
    public required IReadOnlyList<string> TypyExternichOdkazu { get; set; }
    public string UiContext { get; set; } = "project";
    public int? MeetingId { get; set; }
    public string? ReturnUrl { get; set; }
    public string BackUrl { get; set; } = string.Empty;
    public string ActiveEditorTab { get; set; } = "basic";
    public bool UseAjaxSubmit { get; set; } = true;
    public string FormAction { get; set; } = "Save";
    public string FormController { get; set; } = "Zaznamy";
    public string ModalTitle { get; set; } = string.Empty;
    public string PrimaryActionLabel { get; set; } = string.Empty;
    public string? SecondaryNote { get; set; }
    public string ProposalEditorMode { get; set; } = RecordProposalEditorModes.None;
    public bool IsProposalEditor => !string.Equals(ProposalEditorMode, RecordProposalEditorModes.None, StringComparison.OrdinalIgnoreCase);
    public bool IsSchedulePlanProposalEditor => string.Equals(ProposalEditorMode, RecordProposalEditorModes.SchedulePlanProposal, StringComparison.OrdinalIgnoreCase);
    public bool IsProposalDecisionDetail { get; set; }
    public int? ProposalId { get; set; }
    public string? ProposalType { get; set; }
    public string? ProposalState { get; set; }
    public bool CanApproveProposal { get; set; }
    public bool CanRejectProposal { get; set; }
    public bool CanRejectAndEditProposal { get; set; }
    // 7a (2026-06-17): rozhodovací akce přesunuty z panelu do detailu. Návrh ZALOŽENÍ má
    // v detailu „Zamítnout a převzít data" (RejectAndTakeOverCreateProposal); návrh HARMONOGRAMU
    // má „Zamítnout a upravit" (RejectAndEditProposal). Rozlišeno podle typu návrhu.
    public bool CanRejectAndTakeOverProposal { get; set; }
    // CanPrefillProposalForm smazáno 2026-04-23 — EditFromProposal bypass zrušen.
    public string? ProposalSummaryNote { get; set; }
    public IReadOnlyDictionary<string, string> ProposalChangedFieldTooltips { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<int, string> ProposalChangedScheduleTypeTooltips { get; set; } = new Dictionary<int, string>();
    public bool AllowBasicMetadataEdit { get; set; } = true;
    public bool AllowTermDeadlineEdit { get; set; } = true;
    public bool ShowExternalTab { get; set; } = true;
    public bool ShowCollaborationTab { get; set; } = true;
    // 7c (2026-06-17): v návrhu ZALOŽENÍ se u externích vazeb pouze zadávají čísla; harvest
    // (4 datumy, bubliny vyjádření, chat „Vyjádření a termíny") = skutečnost, vznikne až po
    // založení reálného záznamu klasickou cestou. Když true, externí panel skryje harvest UI.
    public bool HideExternalHarvestUi { get; set; }
    public bool HasPendingScheduleProposalLock { get; set; }
    public int? PendingScheduleProposalId { get; set; }
    public string? PendingScheduleProposalMessage { get; set; }
    public bool PendingScheduleProposalLocksTermDeadline { get; set; }
    public bool PendingScheduleProposalLocksSchedule { get; set; }
    public bool CanCreateScheduleProposal { get; set; }
    public string? CreateScheduleProposalUrl { get; set; }
    public bool CanEditRecord { get; set; }
    public bool CanEditScheduleFull { get; set; }
    public bool CanEditScheduleAddOnly { get; set; }

    public bool IsProposalFieldChanged(string fieldKey)
        => !string.IsNullOrWhiteSpace(fieldKey) && ProposalChangedFieldTooltips.ContainsKey(fieldKey);

    public string? GetProposalFieldTooltip(string fieldKey)
        => string.IsNullOrWhiteSpace(fieldKey)
            ? null
            : ProposalChangedFieldTooltips.GetValueOrDefault(fieldKey);

    public bool IsProposalScheduleTypeChanged(int typeId)
        => typeId > 0 && ProposalChangedScheduleTypeTooltips.ContainsKey(typeId);

    public string? GetProposalScheduleTypeTooltip(int typeId)
        => typeId > 0
            ? ProposalChangedScheduleTypeTooltips.GetValueOrDefault(typeId)
            : null;
}

public sealed class SubsystemOptionViewModel
{
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public int DefaultOwnerOsobaId { get; init; }
}

public sealed class ExterniOdkazEditViewModel
{
    public int Id { get; set; }
    public string? Typ { get; set; }
    public string? Cislo { get; set; }
    public decimal? PredpokladanaCena { get; set; }
    public int? VyzvaId { get; set; }
    public string? VyzvaKod { get; set; }
    public bool ZaradidDoVyzvy { get; set; }
    public DateTime? DatumObjednani { get; set; }
    public DateTime? PlanDodani { get; set; }
    public DateTime? DatumDodani { get; set; }
    public DateTime? DatumPrevzeti { get; set; }
    public DateTime? LastHarvestedAt { get; set; }
}
