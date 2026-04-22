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
    public string? Tym { get; set; }
    public string? Typ { get; set; }
    /// <summary>Automaticky detekovaný druh vyjádření (K3/K4/K6/K7/K10) — podbarvení / ikonka.</summary>
    public string? Predikat { get; set; }
    /// <summary>Pořadí kroku, na který je aktuálně navázaná (null = v timeline / buffer).</summary>
    public int? NavazanoNaKrokPoradi { get; set; }
    public Guid? NavazanoNaKrokKey { get; set; }
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
