using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Core harvest logic — čte vyjádření z HOT_VYJADRENI (přes <see cref="PmTracker.ServiceDesk.Contracts.IVyjadreniQueryService"/>),
/// aplikuje textové predikáty (<see cref="HarvestPredicates"/>) a upsertuje
/// <see cref="PmTracker.Web.Models.Entities.ZaznamHarmonogramVyjadreniVazbaEntity"/> řádky.
///
/// Od sd-sync-revise plánu (Task 6) přidána fingerprint detekce (spec §5.2): před drillem
/// harvest kontroluje HOT_ZAZNAMY.datum primary + (MAX(HOT_VYJADRENI.id), COUNT(*)) secondary
/// fingerprint proti LastKnown* sloupcům na <c>ZaznamExterniOdkazEntity</c>. Pokud se nic
/// nezměnilo, drill se přeskočí.
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
    /// Harvestuje všechny externí vazby daného projektového záznamu (T5 trigger
    /// i reaktivní T2/T5/T7/T8 z <see cref="SdReactiveSyncConsumer"/>).
    /// </summary>
    Task HarvestRecordAsync(int zaznamId, CancellationToken ct = default);

    /// <summary>
    /// Direct sync — synchronní harvest jednoho externího odkazu. Volá se z T3 (open modal) a T6
    /// (refresh button). Od <see cref="HarvestTicketAsync"/> se liší tím, že projde fingerprint
    /// checkem; pokud fingerprint říká no-change, drill se přeskočí a výsledek je <c>Empty</c>.
    /// </summary>
    Task<VyjadreniHarvestResult> HarvestSingleTicketAsync(int externiOdkazId, CancellationToken ct = default);

    /// <summary>
    /// Harvest celého scope (periodic tick — T4). Fingerprint batch check + drill pro changed
    /// tickety. Vrátí summary pro <c>LastResultJson</c>.
    /// </summary>
    Task<SdHarvestResult> HarvestScopeAsync(HarvestScope scope, SyncTriggerKind trigger, CancellationToken ct = default);

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
