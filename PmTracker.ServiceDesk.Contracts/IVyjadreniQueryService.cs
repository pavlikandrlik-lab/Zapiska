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
}
