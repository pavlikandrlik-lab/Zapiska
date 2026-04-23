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
    /// <param name="ct">Cancellation token.</param>
    /// <param name="source">
    /// Zdroj triggeru (pro diagnostiku / telemetrii — queue dedup ho ignoruje, protože harvest
    /// je idempotentní nezávisle na zdroji). Výchozí <see cref="SdReactiveSource.RecordSave"/> (T2).
    /// </param>
    Task ScheduleHarvestAsync(int externiOdkazId, CancellationToken ct = default, SdReactiveSource source = SdReactiveSource.RecordSave);

    /// <summary>
    /// T5, T8 — harvest všech externích vazeb daného projektového záznamu
    /// (při otevření editoru nebo lazy otevření tabu).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="source">
    /// Zdroj triggeru (pro diagnostiku / telemetrii). Výchozí <see cref="SdReactiveSource.EditorOpen"/> (T5).
    /// </param>
    Task ScheduleHarvestForRecordAsync(int zaznamId, CancellationToken ct = default, SdReactiveSource source = SdReactiveSource.EditorOpen);
}
