namespace PmTracker.Web.Services.Search;

public sealed class SearchQueryRequest
{
    public string Query { get; set; } = string.Empty;
    public IReadOnlyList<float>? QueryEmbedding { get; set; }
    public IReadOnlyList<int>? PreFilterProjectIds { get; set; }
    public int Size { get; set; } = 60;
}

public sealed class SearchHit
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public int? ProjektId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
    public IReadOnlyDictionary<string, string?> Meta { get; set; } = new Dictionary<string, string?>();
}

public sealed class SearchQueryResponse
{
    public IReadOnlyList<SearchHit> Hits { get; set; } = Array.Empty<SearchHit>();
    public long TotalCandidates { get; set; }
}
