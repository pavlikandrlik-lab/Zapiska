namespace PmTracker.Web.Services.ServiceDesk.Prodleni;

/// <summary>Proč penále nejde spočítat (sekce „Nelze vyhodnotit").</summary>
public enum PenaltyChyba
{
    /// <summary>Ticket nemá žádnou předávku.</summary>
    ZadneUdalosti,
    /// <summary>Chybí U1 (založení u dodavatele).</summary>
    ChybiIniciacniUdalost,
    /// <summary>NES: důležitost nemá řádek v <c>ticket_external_sla</c> (spec §16 R6 — originál by dosadil 0 h).</summary>
    SlaChybiProDulezitost,
    /// <summary>Typ záznamu není NES/PMP/PNF.</summary>
    NeznamyTyp,
}

/// <summary>Výsledek výpočtu pro jednoho dodavatele ticketu.</summary>
/// <param name="Dodavatel">Dodavatel, kterému penále patří.</param>
/// <param name="PenalizovaneDny">Penalizované dny (datum bez času) v pořadí započtení.</param>
/// <param name="Duvody">Odůvodnění v pořadí vzniku.</param>
/// <param name="Chyba">Chyba dat, null = spočteno.</param>
/// <param name="PracovniDny">true = penalizují se pracovní dny (NES, PNF), false = kalendářní (PMP).</param>
public sealed record PenaltyResult(string? Dodavatel, IReadOnlyList<DateTime> PenalizovaneDny,
    IReadOnlyList<PenaltyReason> Duvody, PenaltyChyba? Chyba, bool PracovniDny)
{
    /// <summary>Počet penalizovaných dnů (sloupec „Dní penále" ServiceDesku).</summary>
    public int PocetDni => PenalizovaneDny.Count;

    /// <summary>Výpočet skončil chybou dat.</summary>
    public bool MaChybu => Chyba is not null;
}
