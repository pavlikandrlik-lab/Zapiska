namespace PmTracker.Web.Services.Search;

public sealed class SearchDocument
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public int? ProjektId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public IReadOnlyList<string> Keywords { get; set; } = Array.Empty<string>();
    public IReadOnlyList<float>? Embedding { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyDictionary<string, string?> Meta { get; set; } = new Dictionary<string, string?>();

    public string DocumentId => $"{EntityType}:{EntityId}";
}
