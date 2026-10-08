namespace PmTracker.Web.Services.Common;

/// <summary>
/// Čistý text formátovaného obsahu pro hledání (uživatel 2026-10-08): bez HTML značek,
/// s rozbalenými entitami a s mezerami, zalomeními a pevnými mezerami sjednocenými na jednu
/// mezeru. LIKE nad ním najde frázi i přes tučné slovo nebo zalomení řádku. Jediné místo
/// převodu — ukládá ho háček v PmTrackerDbContext a dopočet starých dat.
/// </summary>
public static class RichTextSearchText
{
    // Bezstavová služba (jen statická pravidla sanitizace), sdílená instance je bezpečná.
    private static readonly RichTextContentService RichText = new();

    /// <summary>
    /// <c>null</c> jen pro <c>null</c>. HTML bez viditelného textu dá prázdný řetězec, ne
    /// <c>null</c> — NULL ve sloupci znamená „ještě nedopočteno".
    /// </summary>
    public static string? FromHtml(string? html)
    {
        if (html is null)
        {
            return null;
        }

        return string.Join(' ', RichText.ToPlainText(html)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
