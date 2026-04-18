using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Search;

/// <summary>
/// Implementace IDbSuggestService nad EF Core. Prohledává ProjektoveZaznamy
/// (Nazev, Cil, Popis) a Vyjadreni (TextVyjadreni) pomocí LIKE %q% dotazů.
/// ACL: vrací pouze záznamy projektů, ke kterým má uživatel přístup.
/// </summary>
public sealed class DbSuggestService : IDbSuggestService
{
    private const int MinQueryLength = 2;
    private const int MaxQueryLength = 200;
    private const int SnippetLength = 120;

    private readonly PmTrackerDbContext _db;

    public DbSuggestService(PmTrackerDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SuggestHit>> SuggestAsync(
        string query,
        CurrentUserContextViewModel currentUser,
        int limit,
        CancellationToken cancellationToken)
    {
        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length < MinQueryLength)
        {
            return Array.Empty<SuggestHit>();
        }

        if (trimmed.Length > MaxQueryLength)
        {
            trimmed = trimmed[..MaxQueryLength];
        }

        var likePattern = $"%{EscapeLikePattern(trimmed)}%";
        var halfLimit = Math.Max(1, limit / 2);

        // Projekty do kterých má uživatel přístup (null = superadmin = vše)
        var visibleIds = currentUser.IsSuperAdmin
            ? null
            : currentUser.VisibleProjectIds;

        var zaznamy = await QueryZaznamy(likePattern, visibleIds, halfLimit, cancellationToken);
        var vyjadreni = await QueryVyjadreni(likePattern, visibleIds, limit - zaznamy.Count, cancellationToken);

        var hits = new List<SuggestHit>(zaznamy.Count + vyjadreni.Count);
        hits.AddRange(zaznamy);
        hits.AddRange(vyjadreni);

        return hits;
    }

    private async Task<List<SuggestHit>> QueryZaznamy(
        string likePattern,
        IReadOnlyList<int>? visibleIds,
        int count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return new List<SuggestHit>();
        }

        var q = _db.ProjektoveZaznamy
            .AsNoTracking()
            .Join(
                _db.Projekty.AsNoTracking(),
                z => z.ProjektId,
                p => p.Id,
                (z, p) => new { Zaznam = z, Projekt = p })
            .Where(x =>
                EF.Functions.Like(x.Zaznam.Nazev, likePattern) ||
                (x.Zaznam.Cil != null && EF.Functions.Like(x.Zaznam.Cil, likePattern)) ||
                (x.Zaznam.Popis != null && EF.Functions.Like(x.Zaznam.Popis, likePattern)));

        if (visibleIds != null)
        {
            q = q.Where(x => visibleIds.Contains(x.Zaznam.ProjektId));
        }

        var rows = await q
            .OrderByDescending(x => x.Zaznam.Id)
            .Take(count)
            .Select(x => new
            {
                ZaznamId = x.Zaznam.Id,
                ProjektId = x.Zaznam.ProjektId,
                Nazev = x.Zaznam.Nazev,
                Cil = x.Zaznam.Cil,
                Popis = x.Zaznam.Popis,
                ProjektNazev = x.Projekt.CelyNazev != "" ? x.Projekt.CelyNazev : x.Projekt.Zkratka
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => new SuggestHit
        {
            Type = "zaznam",
            Title = r.Nazev,
            Snippet = BuildSnippet(r.Cil, r.Popis),
            Url = $"/Projekty/Detail/{r.ProjektId}?recordId={r.ZaznamId}",
            ProjektNazev = r.ProjektNazev
        }).ToList();
    }

    private async Task<List<SuggestHit>> QueryVyjadreni(
        string likePattern,
        IReadOnlyList<int>? visibleIds,
        int count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return new List<SuggestHit>();
        }

        // Vyjadreni → Jednani → ProjektId
        var q = _db.Vyjadreni
            .AsNoTracking()
            .Join(
                _db.Jednani.AsNoTracking(),
                v => v.JednaniId,
                j => j.Id,
                (v, j) => new { Vyjadreni = v, Jednani = j })
            .Join(
                _db.Projekty.AsNoTracking(),
                x => x.Jednani.ProjektId,
                p => p.Id,
                (x, p) => new { x.Vyjadreni, x.Jednani, Projekt = p })
            .Where(x => EF.Functions.Like(x.Vyjadreni.TextVyjadreni, likePattern));

        if (visibleIds != null)
        {
            q = q.Where(x => visibleIds.Contains(x.Jednani.ProjektId));
        }

        var rows = await q
            .OrderByDescending(x => x.Vyjadreni.Id)
            .Take(count)
            .Select(x => new
            {
                VyjadreniId = x.Vyjadreni.Id,
                ZaznamId = x.Vyjadreni.ZaznamId,
                ProjektId = x.Jednani.ProjektId,
                Text = x.Vyjadreni.TextVyjadreni,
                ProjektNazev = x.Projekt.CelyNazev != "" ? x.Projekt.CelyNazev : x.Projekt.Zkratka
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => new SuggestHit
        {
            Type = "vyjadreni",
            Title = $"Vyjádření k záznamu #{r.ZaznamId}",
            Snippet = Truncate(r.Text, SnippetLength),
            Url = $"/Projekty/Detail/{r.ProjektId}?recordId={r.ZaznamId}",
            ProjektNazev = r.ProjektNazev
        }).ToList();
    }

    private static string BuildSnippet(string? cil, string? popis)
    {
        var text = !string.IsNullOrWhiteSpace(cil) ? cil : popis;
        return Truncate(text ?? string.Empty, SnippetLength);
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }
        return text[..maxLength].TrimEnd() + "…";
    }

    /// <summary>Escapuje speciální LIKE znaky aby byly brány doslova.</summary>
    private static string EscapeLikePattern(string raw)
        => raw.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
