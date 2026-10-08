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

    /// <summary>
    /// Kratší slovo vedle delších se nehledá ani nepodsvítí. Spojky a předložky („a“, „v“,
    /// „na“) by jinak jako LIKE '%a%' prošly téměř vším a detail by podsvítil každé „a“ na
    /// kartě (uživatel 2026-10-08: „vyhledávání se rozpadne na hledání po písmenech“).
    /// Dotaz jen z krátkých slov („50 %“) se hledá celý. Protějšek v JS: searchHighlight.js.
    /// </summary>
    public const int MinTermLength = 3;

    /// <summary>Strop počtu hledaných výrazů (slov a frází), aby dotaz nerostl bez hranic.</summary>
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

    /// <summary>
    /// Uvozovky, které ohraničují frázi: rovné i typografické (české „…“, anglické “…”).
    /// Každá přepíná dovnitř a ven, takže sedí i česká „ s uzavírací “.
    /// </summary>
    private static readonly char[] QuoteChars = { '"', '\u201E', '\u201C', '\u201D' };

    /// <summary>
    /// Rozloží dotaz na hledané výrazy (uživatel 2026-10-08, jako Google): text v uvozovkách
    /// je jedna fráze hledaná jako celek, ostatní text se dělí na slova. Fráze se hledá vždy —
    /// je to výslovná volba, i když je krátká („"IS"“). Krátké slovo vedle fráze nebo delšího
    /// slova se vynechá, viz <see cref="MinTermLength"/>. Neuzavřená fráze běží do konce
    /// dotazu, aby našeptávání fungovalo už během psaní. Výrazy jdou v pořadí dotazu, fráze
    /// se do <see cref="MaxTerms"/> počítá jako jeden. Protějšek v JS: highlightTerms
    /// v searchHighlight.js.
    /// </summary>
    public static IReadOnlyList<string> SplitTerms(string? query)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < MinQueryLength)
        {
            return Array.Empty<string>();
        }

        var parts = new List<(string Text, bool IsPhrase)>();
        var segments = trimmed.Split(QuoteChars);
        for (var i = 0; i < segments.Length; i++)
        {
            var words = segments[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            // Liché úseky leží mezi uvozovkami (i neuzavřený poslední).
            if (i % 2 == 1)
            {
                if (words.Length > 0)
                {
                    parts.Add((string.Join(' ', words), true));
                }
            }
            else
            {
                parts.AddRange(words.Select(word => (word, false)));
            }
        }

        var hasLongTerm = parts.Any(part => part.IsPhrase || part.Text.Length >= MinTermLength);
        return parts
            .Where(part => part.IsPhrase || !hasLongTerm || part.Text.Length >= MinTermLength)
            .Select(part => part.Text)
            .Take(MaxTerms)
            .ToList();
    }

    /// <summary>
    /// Výrazy zpět jako dotaz pro podsvícení na detailu (parametr hl). Fráze a krátké výrazy
    /// jdou v uvozovkách — jinak by je podsvícení rozložilo na slova, resp. vynechalo —, takže
    /// se na kartě podsvítí přesně to, podle čeho se hledalo.
    /// </summary>
    public static string ToHighlightQuery(IEnumerable<string> terms) => string.Join(' ', terms.Select(term =>
        term.Contains(' ') || term.Length < MinTermLength ? $"\"{term}\"" : term));

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

    /// <summary>
    /// Konec <paramref name="text"/> o <paramref name="count"/> slovech — jako <b>výsek</b>
    /// původního textu, ne jako slova poskládaná mezerou.
    /// <para>
    /// Na rozdílu záleží: LIKE hledá podřetězec, takže shoda běžně padne doprostřed slova
    /// nebo čísla. Skládání ze slov by pak do dat vložilo mezeru, která v nich není —
    /// z „123456" by bylo „1234 56" a ze „Zálohování" „Zá loho vání". Výsek nechá
    /// oddělovače přesně tak, jak byly.
    /// </para>
    /// </summary>
    private static string TakeLastWords(string text, int count)
    {
        if (string.IsNullOrEmpty(text) || count <= 0)
        {
            return string.Empty;
        }

        var start = text.Length;
        for (var word = 0; word < count; word++)
        {
            while (start > 0 && char.IsWhiteSpace(text[start - 1]))
            {
                start--;
            }

            if (start == 0)
            {
                break;
            }

            while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
            {
                start--;
            }
        }

        return text[start..];
    }

    /// <summary>
    /// Začátek <paramref name="text"/> o <paramref name="count"/> slovech, jako výsek.
    /// Platí totéž co pro <see cref="TakeLastWords"/> — viz tamní poznámka.
    /// </summary>
    private static string TakeFirstWords(string text, int count)
    {
        if (string.IsNullOrEmpty(text) || count <= 0)
        {
            return string.Empty;
        }

        var end = 0;
        for (var word = 0; word < count; word++)
        {
            while (end < text.Length && char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            if (end == text.Length)
            {
                break;
            }

            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }
        }

        return text[..end];
    }
}
