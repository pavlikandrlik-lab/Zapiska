namespace PmTracker.Web.Services.ServiceDesk.Prodleni;

/// <summary>Stav dat přehledu prodlení (spec 2026-10-15 §13).</summary>
public enum ProdleniStavDat
{
    /// <summary>Spočteno.</summary>
    Ok,
    /// <summary>Projekt bez IS nebo bez napojení na databázi ServiceDesku → placeholder.</summary>
    BezNapojeni,
    /// <summary>Databáze neodpovídá / chybí právo → chybová karta „Zkusit znovu".</summary>
    Nedostupne,
}

/// <summary>Ticket právě u dodavatele s penalizovanými dny (jeden řádek na dodavatele).</summary>
/// <param name="UDodavateleOd">Datum poslední předávky dodavateli (U1/U2).</param>
/// <param name="Termin">NES: okamžik vyčerpání SLA; PMP/PNF: termín plnění aktuální obrátky.</param>
/// <param name="ProdlouzenoDo">Nejvyšší schválený termín (U11), když nějaký je.</param>
/// <param name="PenalizovaneDny">Všechny penalizované dny ticketu u daného dodavatele (i z dřívějších obrátek).</param>
/// <param name="PracovniDny">true = pracovní dny (NES, PNF), false = kalendářní (PMP).</param>
/// <param name="Udalosti">Předávky ticketu seřazené (Datum, Id) — pro časovou osu.</param>
public sealed record ProdleniPolozka(int TicketId, string Pid, string TypZaznamu, string? Dulezitost, string? Strucne,
    string? Dodavatel, bool ViceDodavatelu, DateTime UDodavateleOd, DateTime? Termin, DateTime? ProdlouzenoDo,
    int PenalizovaneDny, IReadOnlyList<DateTime> PenalizovaneDnyDatumy, bool PracovniDny,
    IReadOnlyList<WorkflowUdalost> Udalosti, IReadOnlyList<PenaltyReason> Duvody);

/// <summary>Ticket u dodavatele bez penále, kterému se blíží termín (NES ≤ 48 h, PMP/PNF ≤ 5 dní).</summary>
public sealed record HroziPolozka(int TicketId, string TypZaznamu, string? Strucne, string? Dodavatel, DateTime Termin, TimeSpan Zbyva);

/// <summary>Ticket u dodavatele, jehož penále nejde spočítat (chyba dat).</summary>
public sealed record NelzeVyhodnotitPolozka(int TicketId, string TypZaznamu, string? Strucne, string? Dulezitost, PenaltyChyba Chyba);

/// <summary>Nearchivovaný synchronizovaný ticket — Statistiky, otevřené tickety podle typu (vizuál 16).</summary>
public sealed record SynchronizovanyTicket(int TicketId, string TypZaznamu, bool UDodavatele);

/// <summary>Penalizované dny ticketu u dodavatele (i vrácené tickety) — Statistiky (vizuály 17, 18).</summary>
public sealed record PenalizaceVObdobi(int TicketId, string? Dodavatel, IReadOnlyList<DateTime> Dny);

/// <summary>Přehled prodlení IS projektu pro záložku NES v prodlení, souhrnný pás, Statistiky a Excel.</summary>
public sealed record ProdleniPrehled(ProdleniStavDat Stav, int? IsId, string? IsZkratka,
    IReadOnlyList<ProdleniPolozka> VProdleni, IReadOnlyList<HroziPolozka> Hrozi,
    IReadOnlyList<NelzeVyhodnotitPolozka> NelzeVyhodnotit,
    IReadOnlyList<SynchronizovanyTicket> Otevrene, IReadOnlyList<PenalizaceVObdobi> Penalizace)
{
    /// <summary>Součet penalizovaných dnů ticketů v prodlení.</summary>
    public int SoucetDni => VProdleni.Sum(p => p.PenalizovaneDny);

    /// <summary>Přehled bez dat (bez napojení nebo při výpadku).</summary>
    public static ProdleniPrehled Prazdny(ProdleniStavDat stav, int? isId, string? isZkratka)
        => new(stav, isId, isZkratka, [], [], [], [], []);
}

/// <summary>Přehled prodlení projektu (přes jeho IS).</summary>
public interface IProdleniService
{
    /// <summary>Aktuální stav: nearchivované synchronizované tickety IS.</summary>
    Task<ProdleniPrehled> GetPrehledAsync(int projektId, CancellationToken ct);

    /// <summary>Pro Statistiky: navíc archivované tickety s předávkou od <paramref name="obdobiOd"/>.</summary>
    Task<ProdleniPrehled> GetPrehledProObdobiAsync(int projektId, DateTime obdobiOd, CancellationToken ct);
}
