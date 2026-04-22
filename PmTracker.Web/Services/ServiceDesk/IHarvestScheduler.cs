namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Plánovač harvestu vyjádření z intranetNEW ServiceDesku.
/// Fire-and-forget API — implementace má vrátit okamžitě,
/// skutečný harvest běží na pozadí (reactive queue / Hangfire dle implementace).
/// </summary>
/// <remarks>
/// Reálnou implementaci dodává <see cref="ReactiveHarvestSchedulerAdapter"/> —
/// zapisuje do <c>IReactiveSyncQueue&lt;SdReactiveHarvestRequest&gt;</c>, skutečný
/// harvest provede <c>SdReactiveSyncConsumer</c>.
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
