namespace PmTracker.Web.Services.Search;

/// <summary>Proč se záznam našel. Určuje, co se vykreslí na druhém řádku dropdownu.</summary>
public enum SearchMatchKind
{
    /// <summary>Shoda v názvu záznamu (nebo v jeho čísle).</summary>
    Nazev,

    /// <summary>Shoda v popisu nebo cíli záznamu.</summary>
    Popis,

    /// <summary>Shoda v textu navázaného vyjádření.</summary>
    Vyjadreni,

    /// <summary>Shoda v čísle navázaného externího odkazu.</summary>
    ExterniOdkaz
}

/// <summary>Stabilní klíče kategorií. Jdou do JSON i do markupu — neměnit.</summary>
public static class SearchCategoryKeys
{
    public const string Zaznamy = "zaznamy";
}

/// <summary>Jeden výsledek. Jednotkou je vždy záznam, i když shoda padla ve vyjádření.</summary>
public sealed record SearchResultItem(
    int ZaznamId,
    int ProjektId,
    string Nazev,
    string? CisloViditelne,
    string SubsystemKod,
    SearchMatchKind MatchKind,
    SearchSnippet? Snippet,
    int? CisloJednani,
    string DetailUrl);

/// <summary>
/// Kategorie výsledků. Dnes je jediná (Záznamy), model ji přesto drží jako kolekci,
/// aby přidání další kategorie bylo doplněním implementace, ne přepisem zobrazení.
/// </summary>
public sealed record SearchResultCategory(
    string Key,
    string Nazev,
    IReadOnlyList<SearchResultItem> Items);

public sealed record SearchResult(
    string Query,
    IReadOnlyList<SearchResultCategory> Categories)
{
    public int TotalCount => Categories.Sum(c => c.Items.Count);

    public static SearchResult Empty(string query) =>
        new(query, Array.Empty<SearchResultCategory>());
}
