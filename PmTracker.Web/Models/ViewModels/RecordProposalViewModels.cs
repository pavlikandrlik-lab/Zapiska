namespace PmTracker.Web.Models.ViewModels;

public static class RecordProposalTypeCodes
{
    public const string CreateRecord = "CREATE_RECORD";
    public const string SchedulePlanChange = "SCHEDULE_PLAN_CHANGE";
}

/// <summary>
/// Plán D — kroky harmonogramu, u kterých skutečnost <b>nepřijde</b> z vytěžování
/// vyjádření (HOT_VYJADRENI), protože nemají textový trigger v ServiceDesku.
/// Skutečnost u těchto kroků se zadává ručně (ManualActualKrokDto).
///
/// Mapování na názvy kroků (podle <c>HarmonogramService.DefaultHarmonogramKroky</c>):
/// <list type="bullet">
/// <item>2 — konzultace termínů s dodavatelem</item>
/// <item>5 — vypořádání připomínek</item>
/// <item>8 — připomínkování</item>
/// <item>9 — testování</item>
/// </list>
/// </summary>
public static class HarmonogramManualSteps
{
    public static readonly IReadOnlySet<int> KrokPoradi = new HashSet<int> { 2, 5, 8, 9 };

    public static bool IsManual(int krokPoradi) => KrokPoradi.Contains(krokPoradi);
}

public static class RecordProposalStateCodes
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";

    /// <summary>
    /// Plán Harmonogram refactor 2026-05-01 (DESIGN-7-D) — auto-supersede vlastního
    /// starého návrhu při novém submitu. UI ho zobrazuje read-only s linkem na nový návrh.
    /// PendingScheduleProposalLockEvaluator filtruje pouze Stav=Pending — Superseded se na
    /// lock check nepodílí.
    /// </summary>
    public const string Superseded = "SUPERSEDED";
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

    /// <summary>
    /// Plán D: ruční skutečnost pro kroky 2/5/8/9, kam vyjádření neplyne.
    /// Aplikuje se při schválení návrhu do HS0X_DELAY.HodnotaInt jako odchylka
    /// vůči plánu vypočítanému <see cref="Services.Data.HarmonogramService"/>.
    /// </summary>
    public List<ManualActualKrokDto> ManualActualKroky { get; set; } = [];

    /// <summary>
    /// Plán D: předpřipravené vazby bublina-&gt;krok pro nový záznam (schéma 3).
    /// Aplikuje se při schválení návrhu do tabulky <c>zaznam_harmonogram_vyjadreni_vazba</c>
    /// s &lt;c&gt;Source = Manual&lt;/c&gt;.
    /// </summary>
    public List<HarmonogramVazbaDto> HarmonogramVazby { get; set; } = [];
}

public sealed class SchedulePlanProposalPayload
{
    public int ProjektId { get; set; }
    public int ZaznamId { get; set; }
    public DateTime TerminUkonceni { get; set; }
    public List<SaveRecordHarmonogramValueCommand> PlannedHarmonogramHodnoty { get; set; } = [];
    public List<SaveRecordHarmonogramValueCommand> ActualHarmonogramHodnoty { get; set; } = [];
    public bool ChangesTermDeadline { get; set; }
    public bool ChangesSchedulePlan { get; set; }
    public bool ChangesScheduleActual { get; set; }

    /// <summary>
    /// Plán D: ruční skutečnost pro kroky 2/5/8/9 (ty, kam vyjádření v HOT DB
    /// neplyne). Aplikuje se při schválení návrhu do HS0X_DELAY.HodnotaInt
    /// jako odchylka v kalendářních dnech vůči plánovaného konce kroku.
    /// </summary>
    public List<ManualActualKrokDto> ManualActualKroky { get; set; } = [];
}

public sealed class ManualActualKrokDto
{
    public Guid KrokKey { get; set; }

    /// <summary>
    /// Kalendářní datum skutečnosti zadané uživatelem.
    /// <c>DateOnly</c> = bez časové zóny + bez času = žádný DST / Kind / ±1 den shift.
    /// Posílá se z JS jako "yyyy-MM-dd" ISO string; default ASP.NET Core binder
    /// <c>DateOnly</c> přijímá právě tento formát.
    /// </summary>
    public DateOnly AbsolutniDatum { get; set; }
}

public sealed class HarmonogramVazbaDto
{
    public Guid KrokKey { get; set; }

    /// <summary>
    /// Index do <see cref="CreateRecordProposalPayload.ExterniVazby"/> — vazba
    /// na externí odkaz, který se teprve vytvoří při schválení návrhu.
    /// </summary>
    public int ExterniOdkazIndex { get; set; }

    public long HotVyjadreniId { get; set; }

    /// <summary>
    /// Timestamp vyjádření z HOT DB (reálný okamžik, ne kalendářní datum).
    /// <c>DateTimeOffset</c> = absolutní okamžik včetně offsetu; server konverze
    /// do UTC přes <c>TimeProvider.GetUtcNow()</c>.
    /// </summary>
    public DateTimeOffset DatumVyjadreni { get; set; }
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
    public int ActualStepCount { get; init; }
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool CanRejectAndTakeOver { get; set; }
    public bool CanRejectAndEdit { get; set; }
    public bool CanPrefillCreateForm { get; set; }
    public string? DetailUrl { get; set; }
    public string? ScheduleProposalEditorUrl { get; set; }
    public string? PrefillCreateFormUrl { get; set; }
}
