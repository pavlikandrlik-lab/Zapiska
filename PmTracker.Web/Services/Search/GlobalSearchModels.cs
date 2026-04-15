namespace PmTracker.Web.Services.Search;

public sealed class GlobalSearchResult
{
    public string Query { get; set; } = string.Empty;
    public IReadOnlyList<SearchHit> Hits { get; set; } = Array.Empty<SearchHit>();
    public IReadOnlyDictionary<string, IReadOnlyList<SearchHit>> Groups { get; set; }
        = new Dictionary<string, IReadOnlyList<SearchHit>>();
    public long TotalCandidates { get; set; }
    public bool HasMore { get; set; }
}
