namespace PmTracker.Web.Models.ViewModels;

public static class RecordProposalTypeCodes
{
    public const string CreateRecord = "CREATE_RECORD";
    public const string SchedulePlanChange = "SCHEDULE_PLAN_CHANGE";
}

public static class RecordProposalStateCodes
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}

public static class RecordProposalEditorModes
{
    public const string None = "none";
    public const string CreateProposal = "create-proposal";
    public const string SchedulePlanProposal = "schedule-plan-proposal";
}

public sealed class RecordProposalPayload
{
    public string ProposalType { get; set; } = string.Empty;
    public CreateRecordProposalPayload? CreateRecord { get; set; }
    public SchedulePlanProposalPayload? SchedulePlan { get; set; }
}

public sealed class CreateRecordProposalPayload
{
    public int ProjektId { get; set; }
    public string Kategorie { get; set; } = string.Empty;
    public string? TypUkolu { get; set; }
    public string Stav { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public string? Cil { get; set; }
    public string? Popis { get; set; }
    public int VlastnikId { get; set; }
    public DateTime DatumZalozeni { get; set; }
    public DateTime TerminUkonceni { get; set; }
    public string Subsystem { get; set; } = string.Empty;
    public List<int> VybraniSpolupracovniciIds { get; set; } = [];
    public List<SaveRecordExterniVazbaCommand> ExterniVazby { get; set; } = [];
    public List<SaveRecordHarmonogramValueCommand> HarmonogramHodnoty { get; set; } = [];
    public int? JednaniIdProCislo { get; set; }
}

public sealed class SchedulePlanProposalPayload
{
    public int ProjektId { get; set; }
    public int ZaznamId { get; set; }
    public DateTime TerminUkonceni { get; set; }
    public List<SaveRecordHarmonogramValueCommand> PlannedHarmonogramHodnoty { get; set; } = [];
}

public sealed class ProjektNavrhyTabViewModel
{
    public int ProjektId { get; init; }
    public bool CanCreateRecordProposal { get; set; }
    public bool CanCreateScheduleProposal { get; set; }
    public string? CreateRecordProposalUrl { get; set; }
    public IReadOnlyList<RecordProposalListItemViewModel> NavrhyZalozeni { get; init; } = Array.Empty<RecordProposalListItemViewModel>();
    public IReadOnlyList<RecordProposalListItemViewModel> NavrhyHarmonogramu { get; init; } = Array.Empty<RecordProposalListItemViewModel>();
}

public sealed class RecordProposalListItemViewModel
{
    public int Id { get; init; }
    public int ProjektId { get; init; }
    public int? ZaznamId { get; init; }
    public int? ApprovedRecordId { get; init; }
    public required string TypNavrhu { get; init; }
    public required string TypNavrhuLabel { get; init; }
    public required string Stav { get; init; }
    public required string StavLabel { get; init; }
    public required string Subsystem { get; init; }
    public required string Autor { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? RozhodlUzivatel { get; init; }
    public DateTime? DecidedAt { get; init; }
    public string? Nazev { get; init; }
    public string? Cil { get; init; }
    public string? CisloViditelne { get; init; }
    public DateTime? DatumZalozeni { get; init; }
    public DateTime? TerminUkonceni { get; init; }
    public int PlannedStepCount { get; init; }
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool CanRejectAndTakeOver { get; set; }
    public bool CanPrefillCreateForm { get; set; }
    public string? ScheduleProposalEditorUrl { get; set; }
    public string? PrefillCreateFormUrl { get; set; }
}
