namespace PmTracker.Web.Services.Search;

public interface IEmbeddingService
{
    /// <summary>
    /// Vrátí dense embedding pro daný text, nebo null, pokud embedding není povolený / nedostupný.
    /// </summary>
    Task<IReadOnlyList<float>?> EmbedAsync(string text, CancellationToken cancellationToken);
}
