namespace PmTracker.Web.Services.Sync;

public interface IHasDedupKey
{
    /// <summary>
    /// Klíč pro deduplikaci v queue. Dva requesty se stejným DedupKey se považují
    /// za duplicitní — jen první se do queue dostane, další se tiše drop-nou.
    /// Typicky ID entity, které request dotýká (osobaId, zaznamId).
    /// </summary>
    object DedupKey { get; }
}
