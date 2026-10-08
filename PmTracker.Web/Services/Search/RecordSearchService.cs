using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Search;

/// <summary>
/// Vyhledávání nad ostrými tabulkami. Žádná indexová vrstva — výsledky jsou vždy
/// čerstvé a odpadá celá reindex mašinerie (spec 2026-09-17 §2.4).
///
/// Autorizace je součástí dotazu, ne post-filtr: záznam, na který uživatel nemá
/// právo, se z databáze vůbec nevrátí.
/// </summary>
public sealed class RecordSearchService : IRecordSearchService
{
    /// <summary>Kolik výsledků se vejde do dropdownu.</summary>
    public const int DropdownLimit = 7;

    private readonly PmTrackerDbContext _db;
    private readonly IProjectVisibilityResolver _visibility;
    private readonly IRichTextContentService _richText;
    private readonly ILogger<RecordSearchService> _logger;

    public RecordSearchService(
        PmTrackerDbContext db,
        IProjectVisibilityResolver visibility,
        IRichTextContentService richText,
        ILogger<RecordSearchService> logger)
    {
        _db = db;
        _visibility = visibility;
        _richText = richText;
        _logger = logger;
    }

    public async Task<SearchResult> SearchAsync(
        string? query,
        CurrentUserContextViewModel user,
        int limit,
        CancellationToken cancellationToken)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        var terms = SearchQueryText.SplitTerms(trimmed);
        if (terms.Count == 0)
        {
            return SearchResult.Empty(trimmed);
        }

        var visibleProjectIds = _visibility.Resolve(user);
        if (visibleProjectIds is { Count: 0 })
        {
            // Prázdná množina znamená žádné výsledky. Kdyby se tady jen „neaplikoval
            // filtr", uživatel bez projektů by uviděl všechno.
            return SearchResult.Empty(trimmed);
        }

        var q = _db.ProjektoveZaznamy.AsNoTracking();

        if (visibleProjectIds is not null)
        {
            q = q.Where(z => visibleProjectIds.Contains(z.ProjektId));
        }

        const string coll = SearchQueryText.AccentInsensitiveCollation;

        foreach (var term in terms)
        {
            var pattern = SearchQueryText.ToContainsPattern(term);

            q = q.Where(z =>
                EF.Functions.Like(EF.Functions.Collate(z.Nazev, coll), pattern)
                || (z.CisloViditelne != null
                    && EF.Functions.Like(EF.Functions.Collate(z.CisloViditelne, coll), pattern))
                || (z.Cil != null && EF.Functions.Like(EF.Functions.Collate(z.Cil, coll), pattern))
                || (z.Popis != null && EF.Functions.Like(EF.Functions.Collate(z.Popis, coll), pattern))
                || _db.Vyjadreni.Any(v => v.ZaznamId == z.Id
                    && EF.Functions.Like(EF.Functions.Collate(v.TextVyjadreni, coll), pattern))
                || _db.ZaznamExterniOdkazy.Any(o => o.ZaznamId == z.Id
                    && EF.Functions.Like(EF.Functions.Collate(o.Cislo, coll), pattern)));
        }

        var rows = await q
            .OrderBy(z => z.Nazev)
            .ThenBy(z => z.Id)
            .Take(limit)
            .Select(z => new
            {
                z.Id,
                z.ProjektId,
                z.Nazev,
                z.CisloViditelne,
                z.Cil,
                z.Popis,
                SubsystemKod = _db.Subsystemy
                    .Where(s => s.Id == z.SubsystemId)
                    .Select(s => s.Kod)
                    .FirstOrDefault(),
                Vyjadreni = _db.Vyjadreni
                    .Where(v => v.ZaznamId == z.Id)
                    .OrderBy(v => v.Id)
                    .Select(v => new
                    {
                        v.Id,
                        v.TextVyjadreni,
                        CisloJednani = _db.Jednani
                            .Where(j => j.Id == v.JednaniId)
                            .Select(j => (int?)j.CisloJednani)
                            .FirstOrDefault()
                    })
                    .ToList(),
                ExterniCisla = _db.ZaznamExterniOdkazy
                    .Where(o => o.ZaznamId == z.Id)
                    .OrderBy(o => o.Id)
                    .Select(o => o.Cislo)
                    .ToList()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<SearchResultItem>(rows.Count);

        foreach (var row in rows)
        {
            // Pořadí rozhoduje, co se ukáže na druhém řádku: nejdřív to, co uživatel
            // vidí na prvním (název, číslo), pak popis, pak vyjádření, nakonec odkaz.
            var kind = SearchMatchKind.Nazev;
            SearchSnippet? snippet =
                SearchQueryText.BuildSnippet(row.Nazev, terms)
                ?? SearchQueryText.BuildSnippet(row.CisloViditelne, terms);

            if (snippet is null)
            {
                snippet = SearchQueryText.BuildSnippet(PlainText(row.Popis), terms)
                          ?? SearchQueryText.BuildSnippet(row.Cil, terms);
                if (snippet is not null)
                {
                    kind = SearchMatchKind.Popis;
                }
            }

            int? cisloJednani = null;
            int? vyjadreniId = null;

            if (snippet is null)
            {
                foreach (var v in row.Vyjadreni)
                {
                    snippet = SearchQueryText.BuildSnippet(PlainText(v.TextVyjadreni), terms);
                    if (snippet is not null)
                    {
                        kind = SearchMatchKind.Vyjadreni;
                        cisloJednani = v.CisloJednani;
                        vyjadreniId = v.Id;
                        break;
                    }
                }
            }

            if (snippet is null)
            {
                foreach (var cislo in row.ExterniCisla)
                {
                    snippet = SearchQueryText.BuildSnippet(cislo, terms);
                    if (snippet is not null)
                    {
                        kind = SearchMatchKind.ExterniOdkaz;
                        break;
                    }
                }
            }

            items.Add(new SearchResultItem(
                ZaznamId: row.Id,
                ProjektId: row.ProjektId,
                Nazev: row.Nazev,
                CisloViditelne: row.CisloViditelne,
                SubsystemKod: row.SubsystemKod ?? string.Empty,
                MatchKind: kind,
                Snippet: snippet,
                CisloJednani: cisloJednani,
                VyjadreniId: vyjadreniId,
                DetailUrl: BuildDetailUrl(row.ProjektId, row.Id, vyjadreniId, terms)));
        }

        _logger.LogDebug("Vyhledávání '{Query}': {Count} výsledků.", trimmed, items.Count);

        return new SearchResult(trimmed,
        [
            new SearchResultCategory(SearchCategoryKeys.Zaznamy, "Záznamy", items)
        ]);
    }

    /// <summary>
    /// Odkaz na záznam v detailu projektu. Detail podle vyjadreniId otevře vyjádření se
    /// shodou (i když není mezi prvními načtenými) a slova z hl na kartě dočasně podsvítí.
    /// </summary>
    private static string BuildDetailUrl(int projektId, int zaznamId, int? vyjadreniId, IReadOnlyList<string> terms)
    {
        var url = $"/Projekty/Detail/{projektId}?recordId={zaznamId}";
        if (vyjadreniId is not null)
        {
            url += $"&vyjadreniId={vyjadreniId}";
        }

        // Jen slova, podle kterých se hledalo — ne syrový dotaz se spojkami (viz MinTermLength).
        return url + "&hl=" + Uri.EscapeDataString(string.Join(' ', terms));
    }

    /// <summary>
    /// Popis a vyjádření jsou HTML z editoru (značky, entity jako &amp;amp; a &amp;lt;).
    /// Náhled se staví z prostého textu; konce odstavců a položek seznamu se slijí do
    /// jedné mezery, ať náhled zůstane na jednom řádku.
    /// </summary>
    private string PlainText(string? richText) =>
        string.Join(' ', _richText.ToPlainText(richText)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
