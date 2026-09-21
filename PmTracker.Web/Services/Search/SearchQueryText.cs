using System.Globalization;

namespace PmTracker.Web.Services.Search;

/// <summary>Výřez textu kolem nalezené shody. Zvýraznění vykresluje UI, ne tahle třída.</summary>
public sealed record SearchSnippet(string Before, string Match, string After);

/// <summary>
/// Čistá textová logika vyhledávání — rozklad dotazu, escapování a výřez okolí shody.
/// Bez databáze a bez EF, aby šla celá otestovat unit testy.
/// </summary>
public static class SearchQueryText
{
    /// <summary>Kratší dotaz se do databáze vůbec neposílá.</summary>
    public const int MinQueryLength = 3;

    /// <summary>Strop počtu slov, aby dotaz nerostl bez hranic.</summary>
    public const int MaxTerms = 6;

    /// <summary>
    /// Databáze má Czech_CI_AS (rozlišuje diakritiku). Bez vynucení akcent-necitlivé
    /// collation by „zalohovani" nenašlo „Zálohování".
    /// <para>
    /// Proč NE Czech_CI_AI: čeština bere č, ř, š a ž jako <b>samostatná písmena
    /// abecedy</b>, ne jako diakritické varianty c/r/s/z. Mají proto vlastní primární
    /// váhu, kterou akcent-necitlivost ze své podstaty minout nemůže — pod Czech_CI_AI
    /// i Czech_100_CI_AI dotaz „rizeni" nenajde „Řízení" a „cislo" nenajde „Číslo".
    /// Latin1_General_CI_AI je skládá a zároveň drží á→a, ě→e, ď→d, ů→u, ý→y.
    /// Ověřeno dotazem na SQL Server, viz BuildSnippet_NajdeCeskaPismenaBezDiakritiky.
    /// </para>
    /// </summary>
    public const string AccentInsensitiveCollation = "Latin1_General_CI_AI";

    /// <summary>
    /// Protějšek <see cref="AccentInsensitiveCollation"/> na straně .NET. Musí skládat
    /// přesně totéž co collation v databázi, jinak by se text, který dotaz vrátil,
    /// nepodařilo zvýraznit a uživatel by viděl výsledek bez žlutého podbarvení.
    /// InvariantCulture se s Latin1_General_CI_AI shoduje; cs-CZ by se rozešla
    /// právě na háčcích.
    /// </summary>
    private static readonly CompareInfo AccentFolding = CultureInfo.InvariantCulture.CompareInfo;

    private const CompareOptions AccentInsensitive =
        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static IReadOnlyList<string> SplitTerms(string? query)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < MinQueryLength)
        {
            return Array.Empty<string>();
        }

        return trimmed
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Take(MaxTerms)
            .ToList();
    }

    /// <summary>
    /// Escapuje zástupné znaky LIKE hranatými závorkami — stejně jako zbytek projektu.
    /// Pořadí je podstatné: '[' musí jít první, jinak by se rozbily závorky vložené poté.
    /// </summary>
    public static string EscapeLikePattern(string raw) => raw
        .Replace("[", "[[]")
        .Replace("%", "[%]")
        .Replace("_", "[_]");

    public static string ToContainsPattern(string term) => $"%{EscapeLikePattern(term)}%";

    /// <summary>
    /// Najde první slovo, které se v textu vyskytuje, a vrátí <paramref name="wordsAround"/>
    /// slov před ním a za ním. Hledá bez ohledu na diakritiku a velikost písmen, ale
    /// <see cref="SearchSnippet.Match"/> nese text tak, jak je v datech — zvýrazní se
    /// tedy „Zálohování", i když uživatel napsal „zalohovani".
    /// </summary>
    public static SearchSnippet? BuildSnippet(string? haystack, IReadOnlyList<string> terms, int wordsAround = 2)
    {
        if (string.IsNullOrEmpty(haystack) || terms.Count == 0)
        {
            return null;
        }

        foreach (var term in terms)
        {
            if (string.IsNullOrEmpty(term))
            {
                continue;
            }

            var index = AccentFolding.IndexOf(haystack.AsSpan(), term.AsSpan(), AccentInsensitive, out var matchLength);
            if (index < 0 || matchLength <= 0)
            {
                continue;
            }

            var before = TakeLastWords(haystack[..index], wordsAround);
            var after = TakeFirstWords(haystack[(index + matchLength)..], wordsAround);

            return new SearchSnippet(before, haystack.Substring(index, matchLength), after);
        }

        return null;
    }

    private static string TakeLastWords(string text, int count)
    {
        if (string.IsNullOrEmpty(text) || count <= 0)
        {
            return string.Empty;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        var taken = string.Join(' ', words.TakeLast(count));
        return taken + " ";
    }

    private static string TakeFirstWords(string text, int count)
    {
        if (string.IsNullOrEmpty(text) || count <= 0)
        {
            return string.Empty;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        return " " + string.Join(' ', words.Take(count));
    }
}
