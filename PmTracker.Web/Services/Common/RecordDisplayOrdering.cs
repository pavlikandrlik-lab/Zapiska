namespace PmTracker.Web.Services.Common;

/// <summary>
/// Jediný zdroj pravdy pro řazení záznamů v zobrazení (Záznamy tab) i v tisku (jednání + projekt):
/// primárně kategorie (Informace → Rozhodnutí → Úkol → ostatní), sekundárně viditelné číslo dle
/// jednání. Klíče se aplikují společně jako jeden složený sort.
/// </summary>
public static class RecordDisplayOrdering
{
    /// <summary>CisloViditelneTyp pro číslování dle jednání (873-1).</summary>
    public const byte MeetingNumberType = 1;

    public static int CategoryOrder(string? categoryName)
    {
        var normalized = (categoryName ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("info")) return 1;
        if (normalized.Contains("rozh")) return 2;
        if (normalized.Contains("ukol") || normalized.Contains("úkol")) return 3;
        return 4;
    }

    public static int VisibleNumberPartA(int cisloViditelneA, int cisloZaznamu)
        => cisloViditelneA > 0 ? cisloViditelneA : Math.Max(0, cisloZaznamu);

    public static int VisibleNumberPartB(byte cisloViditelneTyp, int cisloViditelneB)
        => cisloViditelneTyp == MeetingNumberType ? Math.Max(1, cisloViditelneB) : 0;
}
