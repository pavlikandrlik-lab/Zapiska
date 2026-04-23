using System.Collections.Concurrent;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Sdílený per-externiOdkazId semafor registry. Zabraňuje paralelním mutacím vazeb
/// tiketu mezi (a) auto-harvestem (periodic / reactive / direct sync),
/// (b) manuálním drag-and-drop rebalance přes <see cref="VyjadreniModalController"/>,
/// (c) admin re-harvestem. Registrovaný jako <b>singleton</b> — proces-scoped pool,
/// reset na restartu je OK, žádná trvalá data tu neperzistujeme.
/// </summary>
public interface IPerExterniOdkazLockRegistry
{
    /// <summary>
    /// Vrátí sdílený semafor pro daný externí odkaz. Zavolá-li ho více konzumentů se stejným
    /// id, dostanou vždy tutéž instanci (ConcurrentDictionary + value factory).
    /// </summary>
    SemaphoreSlim GetOrAdd(int externiOdkazId);
}

/// <summary>
/// Výchozí implementace <see cref="IPerExterniOdkazLockRegistry"/> — ConcurrentDictionary
/// s lazy semafor factory. Semafory se neuvolňují (TOCTOU vs. ConcurrentDictionary by
/// vyžadovalo reference counting, proces-scoped pool je dostatečný ~1000 tiketů × ~48 B).
/// </summary>
public sealed class PerExterniOdkazLockRegistry : IPerExterniOdkazLockRegistry
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public SemaphoreSlim GetOrAdd(int externiOdkazId)
        => _locks.GetOrAdd(externiOdkazId, _ => new SemaphoreSlim(1, 1));
}
