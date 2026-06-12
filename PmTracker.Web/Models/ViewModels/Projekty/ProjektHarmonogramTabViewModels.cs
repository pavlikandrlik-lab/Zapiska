using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjektHarmonogramTabViewModel
{
    public int ProjektId { get; init; }
    public int CurrentUserOsobaId { get; set; }
    public IReadOnlyList<ProjektHarmonogramUkolViewModel> HarmonogramUkoly { get; init; } = Array.Empty<ProjektHarmonogramUkolViewModel>();

    /// <summary>
    /// Sdílený filter shell — identický s <see cref="ProjektZaznamyTabViewModel.FilterShell"/>.
    /// Render přes <c>_ProjectFilterShell.cshtml</c>. Spec 2026-04-30-project-filter-unification-design.
    /// </summary>
    public ProjectFilterShellViewModel FilterShell { get; init; } = new();
}

public sealed class ProjektHarmonogramUkolViewModel
{
    public int ZaznamId { get; init; }
    public required string CisloViditelne { get; init; }
    public required string Nazev { get; init; }
    public string? TypUkolu { get; init; }
    public string? TypUkoluKod { get; init; }
    public required string Stav { get; init; }
    public string? StavKod { get; init; }
    public required string SubsystemKod { get; init; }
    public required string Subsystem { get; init; }
    public int SubsystemPoradi { get; init; }
    public bool SubsystemHasProjectOrder { get; init; }
    public required string Vlastnik { get; init; }
    public int VlastnikOsobaId { get; init; }
    public required string KategorieKod { get; init; }
    public required string Kategorie { get; init; }
    public bool JeAktivni { get; init; } = true;
    public IReadOnlyList<string> JednaniVyjadreniStavyKody { get; init; } = Array.Empty<string>();
    public bool Stihame { get; init; }
    public HarmonogramBlockViewModel HarmonogramBlok { get; init; } = new();
    public bool CanManageSchedule { get; set; }
    public bool CanCreateScheduleProposal { get; set; }
    public string? ScheduleEditUrl { get; set; }
    public string? ScheduleProposalUrl { get; set; }
}

public sealed class HarmonogramBlockViewModel
{
    public int RecordId { get; init; }
    public string Mode { get; init; } = "project-readonly";
    public DateTime DatumZalozeni { get; init; }
    public DateTime TerminUkonceni { get; init; }
    public string DelayBarvaHex { get; init; } = "#dc2626";
    public HarmonogramSouhrnViewModel Souhrn { get; init; } = new();
    public IReadOnlyList<HarmonogramKrokEditViewModel> Kroky { get; init; } = Array.Empty<HarmonogramKrokEditViewModel>();
    public ScheduleEditorPermissionSet Permissions { get; set; } = ScheduleEditorPermissionSet.ForReadOnly();
    public IReadOnlyDictionary<int, string> EditorChangedTypeTooltips { get; set; } = new Dictionary<int, string>();
    public string ScheduleVersion { get; init; } = string.Empty;

    /// <summary>
    /// Plán D Task 8: KrokKey kroků, u kterých je ruční skutečnost v pending
    /// návrhu. UI je musí v režimu přímé editace (schéma 1) uzamknout, aby
    /// přímý zápis nekolidoval s čekajícím návrhem.
    /// Výchozí stav je prázdná množina — pro read-only / non-editor módy není
    /// třeba lock řešit (input se stejně neinrenderuje).
    /// </summary>
    public IReadOnlySet<int> LockedManualKrokKeys { get; set; } = new HashSet<int>();

    /// <summary>
    /// Plán D Task 8: UI vlastník potřebuje vědět, zda uživatel smí editovat
    /// ruční skutečnost přímo (mimo návrhový workflow). Odvozeno ze složení
    /// <c>records.edit</c> oprávnění + absence schedule lock.
    /// Default je <c>false</c> (read-only) — composition musí explicitně povolit.
    /// </summary>
    public bool CanEditManualActual { get; set; }
}

public sealed class HarmonogramKrokEditViewModel
{
    public int KrokIndex { get; init; }
    public required string Nazev { get; init; }
    public required string BarvaHex { get; init; }
    public int TrvaniTypId { get; init; }
    public int ZpozdeniTypId { get; init; }
    public int TrvaniDni { get; init; }
    /// <summary>
    /// Phase 9 (DESIGN-10-A, 2026-05-01): nullable.
    /// NULL = "krok ještě nenastal / nebyl vyplněn".
    /// 0 = "vše šlo dle plánu" (krok dokončen včas).
    /// non-0 = OdchylkaDni dnů (kladné = zpoždění, záporné = předstih).
    /// </summary>
    public int? OdchylkaDni { get; init; }
    public int? ZpozdeniDni => OdchylkaDni;
    public DateTime BaselineDatum { get; init; }
    public DateTime SkutecneDatum { get; init; }
    public DateTime PosunuteDatum => SkutecneDatum;

    /// <summary>
    /// Plán D Task 8: stabilní GUID identifikátor kroku schématu, kterým UI
    /// form POST identifikuje ruční skutečnost (<c>ManualActualKroky[i].KrokKey</c>).
    /// Default <see cref="Guid.Empty"/> pro zpětnou kompatibilitu s existujícími
    /// call-sity, které KrokKey zatím neposkytují.
    /// </summary>
    public Guid KrokKey { get; init; }

    /// <summary>
    /// Plán D Task 8: odkud pochází skutečnost — <c>None</c> (nenavázáno),
    /// <c>FromVyjadreni</c> (Active vazba HOT_VYJADRENI),
    /// <c>Manual</c> (HS0X_DELAY vyplněn ručně).
    /// </summary>
    public ZdrojSkutecnosti ZdrojSkutecnosti { get; init; } = ZdrojSkutecnosti.None;

    /// <summary>Plán D Task 8: pro <c>FromVyjadreni</c> HOT_VYJADRENI id pro chat modal routing.</summary>
    public long? SourceVyjadreniId { get; init; }

    /// <summary>Plán D Task 8: datum vyjádření pro tooltip „Z vyjádření {datum}".</summary>
    public DateTime? SourceVyjadreniDatum { get; init; }

    /// <summary>Plán D Task 8: externí odkaz ID, na který chat ikona míří (<c>data-external-odkaz-id</c>).</summary>
    public int? SourceExterniOdkazId { get; init; }

    /// <summary>
    /// Plán D Task 8: zda krok patří do <see cref="HarmonogramManualSteps.KrokPoradi"/>
    /// {2, 5, 8, 9} — UI podle toho rozhoduje, zda vůbec zvažovat render manual inputu.
    /// </summary>
    public bool IsManualKrok { get; init; }

    /// <summary>
    /// Plán 4 Feature C Task 6 — ID řádku <c>zaznam_harmonogram_hodnoty</c> pro HS0X_DELAY
    /// sloupec tohoto kroku. UI ho použije v POST payloadu pro
    /// <c>HarmonogramController.ToggleRezim</c> / <c>SelectCandidate</c>.
    /// <c>null</c> znamená, že HS0X_DELAY řádek ještě neexistuje (krok je na plánové hodnotě,
    /// není zapsaná skutečnost) — toggle / dropdown nelze volat, UI je skryje.
    /// </summary>
    public int? DelayHodnotaId { get; init; }

    /// <summary>
    /// Plán 4 Feature C Task 6 — režim zdroje skutečnosti pro UI switch Auto ⇄ Ručně.
    /// Default <see cref="SkutecnostRezimEnum.Auto"/> pokud HS0X_DELAY řádek nexistuje.
    /// </summary>
    public SkutecnostRezimEnum SkutecnostRezim { get; init; } = SkutecnostRezimEnum.Auto;

    /// <summary>
    /// Plán 4 Feature C Task 6 — audit zdroj datumu (Neznamo/Automat/Manual/Historicka).
    /// UI podle toho zobrazí badge ikonku (🤖/✍️/📜/—).
    /// </summary>
    public SkutecnostZdrojEnum SkutecnostZdroj { get; init; } = SkutecnostZdrojEnum.Neznamo;

    /// <summary>
    /// Plán 4 Feature C Task 6 — user-preferred binding ze dropdown výběru (pokud 2+ kandidáti).
    /// <c>null</c> = použít default MAX.
    /// </summary>
    public int? PreferredExterniOdkazId { get; init; }

    /// <summary>
    /// Plán 4 Feature C Task 6 — zda user má oprávnění přepínat režim / vybírat kandidát.
    /// Default <c>false</c> (read-only) — composition musí explicitně povolit dle
    /// <see cref="PermissionKeys.RecordsScheduleEdit"/>.
    /// </summary>
    public bool CanToggleRezim { get; init; }

    /// <summary>
    /// Plán 4 Feature C Task 6 — kandidátní bindings pro tento krok (predikát-match přes
    /// matici NES/PMP/PNF × KrokPoradi). UI zobrazí chevron ▼ a dropdown pouze pokud
    /// <c>Kandidati.Count &gt; 1</c>. Klik na kandidáta → POST <c>/Harmonogram/SelectCandidate</c>.
    /// Prázdný list = MAX default (nebo žádný kandidát vůbec).
    /// </summary>
    public IReadOnlyList<HarmonogramKrokKandidatViewModel> Kandidati { get; init; }
        = Array.Empty<HarmonogramKrokKandidatViewModel>();
}

/// <summary>
/// Plán 4 Feature C Task 6 — jeden kandidátní binding pro dropdown výběr.
/// </summary>
public sealed class HarmonogramKrokKandidatViewModel
{
    public required int ExterniOdkazId { get; init; }
    public required string Cislo6 { get; init; }
    public required string TypZaznamu { get; init; }
    public required DateTime Datum { get; init; }
    /// <summary>True pokud je tento kandidát aktuálně vybraný (= PreferredExterniOdkazId nebo MAX default).</summary>
    public bool IsSelected { get; init; }
}

/// <summary>
/// Plán D Task 8: wrapper VM pro <c>_ScheduleBlockManualCell.cshtml</c>
/// partial. Drží kontext, který partial potřebuje (krok + composition-level
/// flagy), aby sám partial nemusel dělat žádné lookupy.
/// </summary>
public sealed class ScheduleBlockManualCellViewModel
{
    public required HarmonogramKrokEditViewModel Krok { get; init; }

    /// <summary>Pořadí inputu v rámci <c>ManualActualKroky[i]</c> kolekce ve form POST.</summary>
    public int ManualInputIndex { get; init; }

    public int ZaznamId { get; init; }

    public bool CanEditManualActual { get; init; }

    public bool IsLocked { get; init; }
}

public sealed class HarmonogramSouhrnViewModel
{
    public DateTime BaselineDokonceni { get; init; }
    public DateTime SkutecneDokonceni { get; init; }
    public DateTime PosunuteDokonceni => SkutecneDokonceni;
    public DateTime TerminUkolu { get; init; }
    public int CelkoveTrvaniDni { get; init; }
    public int CelkovaOdchylkaDni { get; init; }
    public int CelkoveZpozdeniDni => CelkovaOdchylkaDni;
    public bool Stihame { get; init; }
    public int PrekroceniDni { get; init; }
}
