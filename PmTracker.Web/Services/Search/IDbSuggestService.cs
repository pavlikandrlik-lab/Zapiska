using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Search;

/// <summary>
/// Rychlý DB suggest (LIKE %q%) nad ProjektoveZaznamy a Vyjadreni — funguje vždy,
/// nezávisle na SearchOptions.Enabled (bez potřeby OpenSearch nebo SearchIndex tabulky).
/// </summary>
public interface IDbSuggestService
{
    Task<IReadOnlyList<SuggestHit>> SuggestAsync(
        string query,
        CurrentUserContextViewModel currentUser,
        int limit,
        CancellationToken cancellationToken);
}

public sealed class SuggestHit
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ProjektNazev { get; set; } = string.Empty;
}
