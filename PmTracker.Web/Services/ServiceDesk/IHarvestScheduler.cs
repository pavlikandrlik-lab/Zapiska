namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Plánovač harvestu vyjádření z intranetNEW ServiceDesku.
/// Fire-and-forget API — implementace má vrátit okamžitě,
/// skutečný harvest běží na pozadí (reactive queue / Hangfire dle implementace).
/// </summary>
/// <remarks>
/// Plán B dodává <see cref="NoOpHarvestScheduler"/>. Plán sd-sync-revise
/// nahradí NoOp za ReactiveHarvestSchedulerAdapter (zapíše do
/// IReactiveSyncQueue&lt;SdReactiveHarvestRequest&gt;). Signatura se nemění.
/// </remarks>
public interface IHarvestScheduler
{
    /// <summary>
    /// T2, T7 — harvest jedné externí vazby (po uložení nebo po schválení návrhu).
    /// </summary>
    Task ScheduleHarvestAsync(int externiOdkazId, CancellationToken ct = default);

    /// <summary>
    /// T5, T8 — harvest všech externích vazeb daného projektového záznamu
    /// (při otevření editoru nebo lazy otevření tabu).
    /// </summary>
    Task ScheduleHarvestForRecordAsync(int zaznamId, CancellationToken ct = default);
}
