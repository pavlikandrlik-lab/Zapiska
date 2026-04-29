namespace PmTracker.Web.Models.ViewModels.Vyjadreni;

/// <summary>
/// View-model pro celý chat modal. Plán C §7.
/// </summary>
public sealed class VyjadreniModalViewModel
{
    public int ExterniOdkazId { get; set; }
    public int ZaznamId { get; set; }
    public int ProjektId { get; set; }
    public string TiketCislo { get; set; } = string.Empty;
    public string? TiketStrucne { get; set; }
    public string? TiketTyp { get; set; }
    public DateTime? LastHarvestedAt { get; set; }
    public bool CanEdit { get; set; }

    public IReadOnlyList<BublinaViewModel> Bubliny { get; set; } = Array.Empty<BublinaViewModel>();
    public IReadOnlyList<StepperKrokViewModel> Kroky { get; set; } = Array.Empty<StepperKrokViewModel>();

    /// <summary>
    /// Plán 1 Feature B — user má právo přidat nepovinný (add-on) krok do harmonogramu
    /// (5-slot buffer pm-chat-stepperu: 3 fixní + 2 add-on).
    /// Klíč <c>PermissionKeys.RecordsScheduleEdit</c> na úrovni projektu.
    /// </summary>
    public bool CanAddAddon { get; set; }

    /// <summary>
    /// Plán 1 Feature B — celkový počet renderovaných slotů stepperu.
    /// 3 fixní chronologické + 2 add-on = 5 (viz buffer.js TOTAL_SLOTS).
    /// </summary>
    public int StepperSlotsPocet { get; init; } = 5;

    /// <summary>Počet fixních chronologických kroků (dle typu ticketu).</summary>
    public int FixniKrokyPocet { get; init; } = 3;

    /// <summary>
    /// Zpráva, pokud je SD integrace vypnutá / tiket nenalezen / modal je prázdný.
    /// UI ji zobrazí jako gov-alert info banner.
    /// </summary>
    public string? EmptyMessage { get; set; }
}

public sealed class BublinaViewModel
{
    public long VyjadreniId { get; set; }
    public DateTime Datum { get; set; }
    public string? Autor { get; set; }
    public string? AutorLogin { get; set; }
    public string? Popis { get; set; }
    /// <summary>
    /// HTML-sanitovaná plain text verze <see cref="Popis"/> pro render v chat bublině.
    /// HOT_VYJADRENI.popis obsahuje HTML (legacy ASP), defaultní Razor escape by zobrazil
    /// literal tagy. Sanitaci dělá <see cref="PmTracker.Web.Services.ServiceDesk.VyjadreniHtmlText"/>.
    /// </summary>
    public string? PopisPlainText { get; set; }
    public string? Tym { get; set; }
    public string? Typ { get; set; }
    /// <summary>Automaticky detekovaný druh vyjádření (K3/K4/K6/K7/K10) — podbarvení / ikonka.</summary>
    public string? Predikat { get; set; }
    /// <summary>Pořadí kroku, na který je aktuálně navázaná (null = v timeline / buffer).</summary>
    public int? NavazanoNaKrokPoradi { get; set; }
    public Guid? NavazanoNaKrokKey { get; set; }

    /// <summary>
    /// Spec 2026-04-29-modal-vyjadreni-dropdown §6 — krok přiřazený této bublině přes binding
    /// (NULL pokud žádný). Pro UI rendering badge nebo dropdownu.
    /// </summary>
    public Guid? AssignedKrokKey { get; set; }
    public int? AssignedKrokPoradi { get; set; }

    /// <summary>"success" pro auto-fill binding, "warning" pro manuál binding, NULL pokud bez bindingu.</summary>
    public string? AssignedKrokColor { get; set; }

    /// <summary>
    /// True pokud je krok auto-pinned (např. K10 PNF na archivní bublině) — UI nezobrazí
    /// ✕ tlačítko pro odebrání.
    /// </summary>
    public bool AssignedKrokIsPinned { get; set; }

    /// <summary>
    /// Dropdown options pro tuto bublinu (jen pokud AssignedKrokKey je NULL).
    /// Disabled options řídí 1:1 / chronologie validation (server-side computed).
    /// </summary>
    public IReadOnlyList<KrokOptionViewModel> StepOptions { get; set; } =
        Array.Empty<KrokOptionViewModel>();
}

public sealed class StepperKrokViewModel
{
    public int KrokPoradi { get; set; }
    public Guid KrokKey { get; set; }
    public string Nazev { get; set; } = string.Empty;
    public string? BarvaHex { get; set; }
    /// <summary>Aktuálně navázané HOT_VYJADRENI.id (Active binding).</summary>
    public long? AktualniVyjadreniId { get; set; }
    /// <summary>Datum aktuálně navázané bubliny — pro chronologie rebalance.</summary>
    public DateTime? AktualniVyjadreniDatum { get; set; }
    /// <summary>Source 1=Auto, 2=Manual — UI vizuálně rozlišuje.</summary>
    public byte? AktualniSource { get; set; }
    /// <summary>
    /// Review finding A-5: ID aktivní vazby (ZaznamHarmonogramVyjadreniVazbaEntity.Id),
    /// aby UI umělo zavolat Delete endpoint bez re-fetch modalu. null = žádná aktivní vazba.
    /// </summary>
    public int? VazbaId { get; set; }
}

/// <summary>Payload pro POST /Vyjadreni/HarmonogramVazba/Create.</summary>
public sealed class CreateVazbaRequest
{
    public int ExterniOdkazId { get; set; }
    public int ZaznamId { get; set; }
    public Guid KrokKey { get; set; }
    public long HotVyjadreniId { get; set; }
    public DateTime DatumVyjadreni { get; set; }
    public int ProjektId { get; set; }
}

/// <summary>Payload pro POST /Vyjadreni/HarmonogramVazba/Delete.</summary>
public sealed class DeleteVazbaRequest
{
    public int VazbaId { get; set; }
    public int ProjektId { get; set; }
}
