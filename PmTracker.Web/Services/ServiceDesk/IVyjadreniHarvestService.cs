namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Core harvest logic — čte vyjádření z HOT_VYJADRENI (přes <see cref="PmTracker.ServiceDesk.Contracts.IVyjadreniQueryService"/>),
/// aplikuje textové predikáty (<see cref="HarvestPredicates"/>) a upsertuje
/// <see cref="PmTracker.Web.Models.Entities.ZaznamHarmonogramVyjadreniVazbaEntity"/> řádky.
/// </summary>
public interface IVyjadreniHarvestService
{
    /// <summary>
    /// Harvestuje vyjádření pro jeden externí odkaz (ticket).
    /// Aplikuje textové predikáty, vytvoří/aktualizuje vazby, aktualizuje
    /// <c>ZaznamExterniOdkazEntity.LastHarvestedAt</c>.
    /// </summary>
    Task<VyjadreniHarvestResult> HarvestTicketAsync(int externiOdkazId, CancellationToken ct = default);

    /// <summary>
    /// Harvestuje všechny externí vazby daného projektového záznamu (T5 trigger).
    /// </summary>
    Task HarvestRecordAsync(int zaznamId, CancellationToken ct = default);

    /// <summary>
    /// Re-harvest — supersedne všechny Active Auto vazby pro daný externí odkaz,
    /// nastaví LastHarvestedAt=NULL, spustí harvest znovu. Manuální vazby
    /// (Source=Manual) zůstávají beze změny.
    /// </summary>
    Task<VyjadreniHarvestResult> ReHarvestTicketAsync(int externiOdkazId, CancellationToken ct = default);
}

/// <summary>
/// Shrnutí jednoho harvest runu — kolik vazeb bylo vytvořeno / superseded / skippnuto.
/// Diagnostická stránka <c>/SDConnector</c> a admin re-harvest tlačítko toto konzumují.
/// </summary>
public sealed record VyjadreniHarvestResult(
    int Fetched,
    int Created,
    int Superseded,
    int Skipped,
    string? Message = null)
{
    public static VyjadreniHarvestResult Empty(string? message = null)
        => new(0, 0, 0, 0, message);
}
