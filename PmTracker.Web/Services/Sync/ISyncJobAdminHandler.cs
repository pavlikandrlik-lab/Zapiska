using PmTracker.Web.Models.ViewModels.Sync;

namespace PmTracker.Web.Services.Sync;

/// <summary>
/// Per-job handler pro admin UI v Nastavení → Synchronizace.
/// Každý konzument (AD, SD active, SD reactive…) registruje jeden implementací
/// těchto tří operací. Controller je dispatching on <see cref="JobKey"/>.
/// </summary>
public interface ISyncJobAdminHandler
{
    string JobKey { get; }

    Task<SyncJobSettingsCardViewModel> LoadCardAsync(bool canManage, CancellationToken ct);

    Task<bool> SaveAsync(SyncJobSettingsInputModel input, int? editorOsobaId, CancellationToken ct);

    /// <summary>
    /// Pokusí se spustit job manuálně. Implementace respektuje 1-min hard floor
    /// (§13.3) a signaluje běžící hosted service přes <see cref="ManualTriggerSignal{TSettings}"/>
    /// (§13.2).
    /// </summary>
    Task<ManualRunOutcome> TriggerManualRunAsync(int? editorOsobaId, CancellationToken ct);
}
