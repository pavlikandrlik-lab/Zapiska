namespace PmTracker.Web.Services.Search;

public sealed class SearchOptions
{
    public const string SectionName = "PmTracker:Search";

    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "OpenSearch";
    public string Uri { get; set; } = "http://localhost:9200";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string IndexName { get; set; } = "pmtracker-search-v1";
    public int ReindexIntervalSeconds { get; set; } = 30;
    public int PostFilterCandidateMultiplier { get; set; } = 3;
    public SearchEmbeddingsOptions Embeddings { get; set; } = new();
}

public sealed class SearchEmbeddingsOptions
{
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "None";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "text-embedding-3-small";
    public int Dimensions { get; set; } = 384;
}
