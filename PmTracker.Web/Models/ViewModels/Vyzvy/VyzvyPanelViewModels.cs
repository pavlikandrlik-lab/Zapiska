namespace PmTracker.Web.Models.ViewModels.Vyzvy;

/// <summary>
/// Panel Výzev. Do 2026-09-07 byl záložkou projektového dashboardu
/// (ProjectDashboardVyzvyPanelViewModel), nově je samostatnou projektovou záložkou —
/// spec docs/superpowers/specs/2026-09-07-vyzvy-dokonceni-design.md §3.
/// </summary>
public sealed class VyzvyPanelViewModel
{
    public int ProjektId { get; init; }
    public bool MuzeEditovat { get; init; }

    /// <summary>Právo na tisk výzvy — klíč vyzvy.word.export kryje Word i PDF (spec §9.1).</summary>
    public bool MuzeTisknout { get; init; }
    public string? ChybaProjektuMessage { get; init; }
    /// <summary>Rok zvolený v railu. Buffer je na roku nezávislý.</summary>
    public int VybranyRok { get; init; }

    /// <summary>Nabídka roků: roky s výzvami, vždy včetně aktuálního roku, sestupně.</summary>
    public IReadOnlyList<int> DostupneRoky { get; init; } = Array.Empty<int>();

    public VyzvyPanelBufferViewModel Buffer { get; init; } = new();
    public IReadOnlyList<VyzvyPanelVyzvaViewModel> Vyzvy { get; init; } = Array.Empty<VyzvyPanelVyzvaViewModel>();

    /// <summary>Kam lze PNF přesunout — buffer a rozpracované výzvy zvoleného roku.</summary>
    public IReadOnlyList<VyzvyCilPresunuViewModel> CilePresunu { get; init; } = Array.Empty<VyzvyCilPresunuViewModel>();
}

/// <summary>
/// Cíl přesunu PNF nabízený v kontextovém menu. VyzvaId == null znamená buffer.
/// Zamčené výzvy (Odesláno, Zrušeno) se do nabídky nedostanou — server by je odmítl
/// a uživatel nemá dostat volbu, která skončí chybou (spec 2026-09-07 §8.3).
/// </summary>
public sealed class VyzvyCilPresunuViewModel
{
    public int? VyzvaId { get; init; }
    public required string Popisek { get; init; }
}


/// <summary>
/// Jeden projektový záznam a jeho PNF v rámci jedné výzvy (nebo bufferu).
/// Záznam se může objevit pod více výzvami — každé jeho PNF může být jinde.
/// </summary>
public sealed class VyzvyPanelZaznamSkupinaViewModel
{
    public int ZaznamId { get; init; }
    public string? CisloViditelne { get; init; }
    public string? Nazev { get; init; }
    public IReadOnlyList<VyzvyPanelPolozkaViewModel> Polozky { get; init; } = Array.Empty<VyzvyPanelPolozkaViewModel>();
}

public sealed class VyzvyPanelBufferViewModel
{
    public IReadOnlyList<VyzvyPanelPolozkaViewModel> Polozky { get; init; } = Array.Empty<VyzvyPanelPolozkaViewModel>();

    /// <summary>PNF seskupené podle projektového záznamu — tvar pro pravý panel.</summary>
    public IReadOnlyList<VyzvyPanelZaznamSkupinaViewModel> Skupiny { get; init; } = Array.Empty<VyzvyPanelZaznamSkupinaViewModel>();
    public bool MuzeZaloztVyzvu { get; init; }
    public string? DuvodBlokace { get; init; }
}

public sealed class VyzvyPanelVyzvaViewModel
{
    public int Id { get; init; }
    public required string Kod { get; init; }
    public int PoradoveVRoce { get; init; }
    public int Rok { get; init; }
    public required string Stav { get; init; }
    public DateTime DatumZalozeni { get; init; }
    public string? ZalozilJmeno { get; init; }
    public DateTime? DatumOdeslani { get; init; }
    public string? OdeslalJmeno { get; init; }
    public IReadOnlyList<VyzvyPanelPolozkaViewModel> Polozky { get; init; } = Array.Empty<VyzvyPanelPolozkaViewModel>();

    /// <summary>PNF seskupené podle projektového záznamu — tvar pro pravý panel.</summary>
    public IReadOnlyList<VyzvyPanelZaznamSkupinaViewModel> Skupiny { get; init; } = Array.Empty<VyzvyPanelZaznamSkupinaViewModel>();

    public decimal? CelkovaCena { get; init; }

    /// <summary>Kolik PNF v součtu nese jen předpokládanou cenu (spec 2026-09-10 R7).</summary>
    public int PocetJenPredpokladanych { get; init; }
    public IReadOnlyList<string> PovoleneStavy { get; init; } = Array.Empty<string>();
    public bool Kolapsovano { get; init; }
}

public sealed class VyzvyPanelPolozkaViewModel
{
    public int ExterniOdkazId { get; init; }
    public int ZaznamId { get; init; }
    public required string Cislo { get; init; }
    public string? StrucneNazev { get; init; }
    public decimal? PredpokladanaCena { get; init; }
    public decimal? KalkulaceCena { get; init; }
    public string? CisloViditelneZaznamu { get; init; }
}

/// <summary>
/// Jeden panel v pravém sloupci. Buffer i výzva sdílí stejný tvar a liší se jen tím,
/// že buffer nemá hlavičku ani součet — díky tomu stačí jeden partial pro obojí.
/// </summary>
public sealed class VyzvyPaneViewModel
{
    /// <summary>Klíč dlaždice: "buffer" nebo "vyzva-{id}". Páruje dlaždici v railu s panelem.</summary>
    public required string Klic { get; init; }
    public bool JeBuffer { get; init; }
    public int ProjektId { get; init; }
    public bool MuzeEditovat { get; init; }
    public bool MuzeTisknout { get; init; }
    public VyzvyPanelVyzvaViewModel? Vyzva { get; init; }
    public IReadOnlyList<VyzvyPanelZaznamSkupinaViewModel> Skupiny { get; init; } = Array.Empty<VyzvyPanelZaznamSkupinaViewModel>();

    /// <summary>Nabídka pro kontextové menu u řádku PNF; aktuální umístění se z ní vyfiltruje.</summary>
    public IReadOnlyList<VyzvyCilPresunuViewModel> CilePresunu { get; init; } = Array.Empty<VyzvyCilPresunuViewModel>();

    public static VyzvyPaneViewModel ProBuffer(VyzvyPanelViewModel panel) => new()
    {
        Klic = "buffer",
        JeBuffer = true,
        ProjektId = panel.ProjektId,
        MuzeEditovat = panel.MuzeEditovat,
        MuzeTisknout = panel.MuzeTisknout,
        Skupiny = panel.Buffer.Skupiny,
        CilePresunu = panel.CilePresunu,
    };

    public static VyzvyPaneViewModel ProVyzvu(VyzvyPanelViewModel panel, VyzvyPanelVyzvaViewModel vyzva) => new()
    {
        Klic = $"vyzva-{vyzva.Id}",
        JeBuffer = false,
        ProjektId = panel.ProjektId,
        MuzeEditovat = panel.MuzeEditovat,
        MuzeTisknout = panel.MuzeTisknout,
        Vyzva = vyzva,
        Skupiny = vyzva.Skupiny,
        CilePresunu = panel.CilePresunu,
    };
}

/// <summary>
/// Formulář Nová výzva (spec 2026-09-07 §5). Číslo se domlouvá se SVA mimo aplikaci,
/// takže ho uživatel zadává ručně — model nese jen kontext, který mu pomůže zadat správné.
/// </summary>
public sealed class NovaVyzvaModalViewModel
{
    public int ProjektId { get; init; }

    /// <summary>Rok, ve kterém výzva vznikne. Vždy aktuální — zpětně se zakládat nedá.</summary>
    public int Rok { get; init; }

    /// <summary>Rok zvolený v railu. Liší-li se od <see cref="Rok"/>, formulář na to upozorní.</summary>
    public int? ProhlizenyRok { get; init; }

    public string? CisloRamcoveSmlouvy { get; init; }

    /// <summary>Čísla už použitá v <see cref="Rok"/> a rámcové smlouvě projektu, vzestupně.</summary>
    public IReadOnlyList<int> ObsazenaCisla { get; init; } = Array.Empty<int>();
}
