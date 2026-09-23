namespace PmTracker.Web.Services.Meetings;

/// <summary>
/// Odhad předvyplněného stavu účasti pro nově zakládané jednání — spec
/// docs/superpowers/specs/2026-09-05-predvyplneni-ucasti-design.md.
/// </summary>
public interface IAttendancePredictor
{
    /// <summary>
    /// Pro každou osobu vrátí id nejčetnějšího stavu účasti z posledních uzavřených
    /// jednání projektu. Osoby bez historie ve výsledku nejsou — volající pro ně
    /// použije výchozí stav.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> PredictAsync(
        int projectId,
        IReadOnlyCollection<int> osobaIds,
        CancellationToken ct = default);
}
