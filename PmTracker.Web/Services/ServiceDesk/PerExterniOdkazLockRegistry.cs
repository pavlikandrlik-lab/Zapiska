using System.Collections.Concurrent;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Sdílený per-externiOdkazId semafor registry. Zabraňuje paralelním mutacím vazeb
/// tiketu mezi (a) auto-harvestem (periodic / reactive / direct sync),
/// (b) manuálním drag-and-drop rebalance přes <see cref="VyjadreniModalController"/>,
/// (c) admin re-harvestem. Singleton proces-scoped — reset na restartu je OK,
/// žádná trvalá data tu neperzistujeme.
/// </summary>
/// <remarks>
/// Velikost ~1000 tiketů × ~48 B = ~48 KB. Nikdy neuvolňujeme semafory, protože
/// ConcurrentDictionary by musela řešit TOCTOU — proces-scoped pool je dostatečný.
/// </remarks>
internal static class PerExterniOdkazLockRegistry
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    public static SemaphoreSlim GetOrAdd(int externiOdkazId)
        => Locks.GetOrAdd(externiOdkazId, _ => new SemaphoreSlim(1, 1));

    /// <summary>Test-only helper, umožňuje testům resetovat state mezi runy.</summary>
    internal static void ClearForTests() => Locks.Clear();
}
