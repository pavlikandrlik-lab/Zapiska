namespace PmTracker.Web.Services.Search;

public sealed class NullEmbeddingService : IEmbeddingService
{
    public Task<IReadOnlyList<float>?> EmbedAsync(string text, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<float>?>(null);
}
