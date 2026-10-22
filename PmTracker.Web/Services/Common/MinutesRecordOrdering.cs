namespace PmTracker.Web.Services.Common;

/// <summary>Klíč řazení záznamu v zápisu jednání (spec 2026-10-22 R5).</summary>
public readonly record struct MinutesOrderKey(
    string Subsystem,
    string SubsystemKod,
    int SubsystemPoradi,
    bool SubsystemHasProjectOrder,
    string Kategorie,
    int CisloViditelneA,
    int CisloViditelneB,
    int CisloZaznamu);

public sealed record MinutesSubsystemGroup<T>(string Subsystem, IReadOnlyList<T> Records);

/// <summary>
/// Pořadí záznamů zápisu (spec 2026-10-22 R5, R7):
/// - skupiny subsystémů: s pořadím v projektu napřed podle pořadí, ostatní za nimi abecedně;
/// - uvnitř skupiny: kategorie (Informace → Rozhodnutí → Úkol → ostatní) → název kategorie →
///   viditelné číslo A → B → číslo záznamu.
/// Jediný zdroj pro tisk (ExportTemplateUseCase) i procházení šipkami (RecordSequenceService).
/// </summary>
public static class MinutesRecordOrdering
{
    public static IReadOnlyList<MinutesSubsystemGroup<T>> GroupBySubsystem<T>(
        IEnumerable<T> records, Func<T, MinutesOrderKey> key, bool useProjectOrder)
        => records
            .Select(record => (Record: record, Key: key(record)))
            .GroupBy(x => new
            {
                x.Key.SubsystemKod,
                Subsystem = string.IsNullOrWhiteSpace(x.Key.Subsystem) ? "-" : x.Key.Subsystem,
                x.Key.SubsystemPoradi,
                x.Key.SubsystemHasProjectOrder
            })
            .OrderBy(group => useProjectOrder ? (group.Key.SubsystemHasProjectOrder ? 0 : 1) : 0)
            .ThenBy(group => useProjectOrder && group.Key.SubsystemHasProjectOrder ? group.Key.SubsystemPoradi : int.MaxValue)
            .ThenBy(group => group.Key.Subsystem, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new MinutesSubsystemGroup<T>(
                group.Key.Subsystem,
                group
                    .OrderBy(x => RecordDisplayOrdering.CategoryOrder(x.Key.Kategorie))
                    .ThenBy(x => x.Key.Kategorie, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(x => x.Key.CisloViditelneA)
                    .ThenBy(x => x.Key.CisloViditelneB)
                    .ThenBy(x => x.Key.CisloZaznamu)
                    .Select(x => x.Record)
                    .ToList()))
            .ToList();

    /// <summary>Záznamy v pořadí zápisu jako jeden seznam (procházení šipkami).</summary>
    public static IReadOnlyList<T> InMinutesOrder<T>(IEnumerable<T> records, Func<T, MinutesOrderKey> key)
        => GroupBySubsystem(records, key, useProjectOrder: true).SelectMany(group => group.Records).ToList();
}
