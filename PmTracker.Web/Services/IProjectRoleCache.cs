namespace PmTracker.Web.Services;

/// <summary>
/// Scoped per-request cache pro „lead-equivalent osoby na projekt/subsystem" lookup.
/// Motivace: <see cref="CommentService"/> + <see cref="ProjectService"/> měli dříve duplikátní
/// implementaci (3 dotazy do DB), kterou volaly opakovaně — při zobrazení detailu projektu
/// jednou per záznam, což vede k desítkám redundantních roundtripů za jeden request.
/// </summary>
/// <remarks>
/// Scope = Scoped (per HTTP request). Cache držíme per-projectId. Data jsou
/// snapshot v okamžiku prvního přístupu; stale read v rámci requestu je OK — authz
/// je seed-only, data se nemění za běhu.
/// </remarks>
public interface IProjectRoleCache
{
    /// <summary>
    /// Vrátí mapping <c>subsystemId → osobaIds</c> pro osoby, které mají na daném projektu
    /// <b>lead nebo deputy_lead</b> roli v libovolném subsystému. Výsledek je cachovaný
    /// po celou dobu HTTP requestu; druhé volání se stejným <paramref name="projectId"/>
    /// vrátí identický dictionary snapshot bez dalšího dotazu do DB.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GetLeadEquivalentOsobaIdsBySubsystemAsync(
        int projectId,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience helper: subset <see cref="GetLeadEquivalentOsobaIdsBySubsystemAsync"/>
    /// pro konkrétní <paramref name="subsystemId"/>. Prázdný list, pokud subsystém nemá
    /// žádného leadera.
    /// </summary>
    Task<IReadOnlyList<int>> GetLeadEquivalentOsobaIdsAsync(
        int projectId,
        int subsystemId,
        CancellationToken ct = default);
}
