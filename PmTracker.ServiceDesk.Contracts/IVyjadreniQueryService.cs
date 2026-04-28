namespace PmTracker.ServiceDesk.Contracts;

/// <summary>
/// Read-only dotazy na HOT_VYJADRENI pro harvest vyjádření.
/// Join na HOT_ZAZNAMY přes pid (ne přes id).
/// </summary>
public interface IVyjadreniQueryService
{
    /// <summary>
    /// Všechna vyjádření pro tiket (6místné HOT_ZAZNAMY.id),
    /// seřazená vzestupně podle data. Volitelně filtrovaná na datum > sinceUtc
    /// (inkrementální harvest).
    /// </summary>
    Task<IReadOnlyList<HotVyjadreniDto>> GetVyjadreniForTicketAsync(
        string cislo6,
        DateTime? sinceUtc,
        CancellationToken ct);

    /// <summary>
    /// Primary fingerprint per ticket: HOT_ZAZNAMY.datum (last-modified) + stav.
    /// Spec §5.2. Vrací jen tickety, které v HOT_ZAZNAMY skutečně existují.
    /// </summary>
    Task<IReadOnlyDictionary<string, HotZaznamFingerprintDto>> GetHotZaznamFingerprintsAsync(
        IReadOnlyCollection<string> cisla6,
        CancellationToken ct);

    /// <summary>
    /// Secondary fingerprint per ticket: MAX(HOT_VYJADRENI.id) + COUNT(*).
    /// Spec §5.2. Klíč je cislo6 (HOT_ZAZNAMY.id). Chybějící tiket = žádný řádek v mapě.
    /// </summary>
    Task<IReadOnlyDictionary<string, VyjadreniSecondaryFingerprintDto>> GetVyjadreniSecondaryFingerprintsAsync(
        IReadOnlyCollection<string> cisla6,
        CancellationToken ct);
}

/// <summary>
/// Primary fingerprint z HOT_ZAZNAMY — spec §5.2. TypZaznamu (PMP/PNF/NES/RU/…)
/// je nutný pro mapování K4_K7_DodaniReseni predikátu na správné pořadí (PMP=4, PNF=7).
/// Review finding Q-10.
/// SlaDeadline (sla_deadline z HOT_ZAZNAMY) se používá jako zdroj <c>PlanDodani</c>
/// pro NES tickety (NES nemá textový predikát PlanDodani — bere se přímo DB sloupec).
/// </summary>
public sealed record HotZaznamFingerprintDto(
    string Cislo,
    DateTime Datum,
    string? Stav,
    string? TypZaznamu = null,
    DateTime? SlaDeadline = null);

/// <summary>
/// Secondary fingerprint z HOT_VYJADRENI — spec §5.2.
/// </summary>
public sealed record VyjadreniSecondaryFingerprintDto(string Cislo, long MaxId, int Count);
