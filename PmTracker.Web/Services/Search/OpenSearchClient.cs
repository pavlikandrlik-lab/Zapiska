using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PmTracker.Web.Services.Search;

public sealed class OpenSearchClient : ISearchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _httpClient;
    private readonly SearchOptions _options;
    private readonly ILogger<OpenSearchClient> _logger;

    public OpenSearchClient(HttpClient httpClient, IOptions<SearchOptions> options, ILogger<OpenSearchClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.Uri.TrimEnd('/') + "/");
        if (!string.IsNullOrEmpty(_options.Username))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
    }

    public async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        var head = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, _options.IndexName), cancellationToken).ConfigureAwait(false);
        if (head.StatusCode == System.Net.HttpStatusCode.OK)
        {
            return;
        }

        var body = OpenSearchIndexSettings.Build(_options.Embeddings.Dimensions, _options.Embeddings.Enabled);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync(_options.IndexName, content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Vytvoření indexu selhalo: {(int)response.StatusCode} {error}");
        }
    }

    public async Task BulkIndexAsync(IReadOnlyCollection<SearchDocument> documents, CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        foreach (var doc in documents)
        {
            sb.Append("{\"index\":{\"_index\":\"").Append(_options.IndexName).Append("\",\"_id\":\"").Append(doc.DocumentId).Append("\"}}\n");
            sb.Append(SerializeDocument(doc)).Append('\n');
        }

        using var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/x-ndjson");
        var response = await _httpClient.PostAsync("_bulk", content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Bulk index selhal: {(int)response.StatusCode} {error}");
        }
    }

    public async Task DeleteDocumentAsync(string entityType, string entityId, CancellationToken cancellationToken)
    {
        var docId = $"{entityType}:{entityId}";
        var response = await _httpClient.DeleteAsync($"{_options.IndexName}/_doc/{Uri.EscapeDataString(docId)}", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Delete dokumentu selhal: {(int)response.StatusCode} {error}");
        }
    }

    public async Task<SearchQueryResponse> SearchAsync(SearchQueryRequest request, CancellationToken cancellationToken)
    {
        var queryJson = BuildSearchQuery(request);
        using var content = new StringContent(queryJson, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_options.IndexName}/_search", content, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Search selhal: {(int)response.StatusCode} {raw}");
        }

        return ParseSearchResponse(raw);
    }

    private static string SerializeDocument(SearchDocument doc)
    {
        var payload = new Dictionary<string, object?>
        {
            ["entity_type"] = doc.EntityType,
            ["entity_id"] = doc.EntityId,
            ["projekt_id"] = doc.ProjektId,
            ["title"] = doc.Title,
            ["body"] = doc.Body,
            ["keywords"] = doc.Keywords,
            ["updated_at"] = doc.UpdatedAt,
            ["meta"] = doc.Meta
        };
        if (doc.Embedding is not null)
        {
            payload["embedding"] = doc.Embedding;
        }
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private string BuildSearchQuery(SearchQueryRequest request)
    {
        var sb = new StringBuilder();
        sb.Append("{\"size\":").Append(request.Size).Append(',');
        sb.Append("\"query\":{\"bool\":{\"must\":[");
        sb.Append("{\"multi_match\":{\"query\":");
        sb.Append(JsonSerializer.Serialize(request.Query));
        sb.Append(",\"fields\":[\"title^3\",\"keywords^5\",\"body\"],\"type\":\"best_fields\",\"fuzziness\":\"AUTO\",\"operator\":\"or\",\"minimum_should_match\":\"60%\"}}");
        sb.Append(']');

        if (request.PreFilterProjectIds is { Count: > 0 })
        {
            sb.Append(",\"filter\":[{\"bool\":{\"should\":[");
            sb.Append("{\"terms\":{\"projekt_id\":");
            sb.Append(JsonSerializer.Serialize(request.PreFilterProjectIds));
            sb.Append("}},");
            sb.Append("{\"bool\":{\"must_not\":[{\"exists\":{\"field\":\"projekt_id\"}}]}}");
            sb.Append("]}}]");
        }

        sb.Append("}}");

        if (request.QueryEmbedding is { Count: > 0 } && _options.Embeddings.Enabled)
        {
            sb.Append(",\"knn\":{\"field\":\"embedding\",\"query_vector\":");
            sb.Append(JsonSerializer.Serialize(request.QueryEmbedding));
            sb.Append(",\"k\":30,\"num_candidates\":100,\"boost\":0.3}");
        }

        sb.Append(",\"highlight\":{\"fields\":{\"title\":{},\"body\":{\"fragment_size\":160}}}");
        sb.Append('}');
        return sb.ToString();
    }

    private static SearchQueryResponse ParseSearchResponse(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var hits = new List<SearchHit>();
        var root = doc.RootElement;
        long total = 0;
        if (root.TryGetProperty("hits", out var hitsRoot))
        {
            if (hitsRoot.TryGetProperty("total", out var totalEl) && totalEl.TryGetProperty("value", out var totalVal))
            {
                total = totalVal.GetInt64();
            }
            if (hitsRoot.TryGetProperty("hits", out var hitsArr) && hitsArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var hit in hitsArr.EnumerateArray())
                {
                    var source = hit.GetProperty("_source");
                    var h = new SearchHit
                    {
                        EntityType = source.TryGetProperty("entity_type", out var et) ? et.GetString() ?? string.Empty : string.Empty,
                        EntityId = source.TryGetProperty("entity_id", out var ei) ? ei.GetString() ?? string.Empty : string.Empty,
                        ProjektId = source.TryGetProperty("projekt_id", out var pi) && pi.ValueKind == JsonValueKind.Number ? pi.GetInt32() : null,
                        Title = source.TryGetProperty("title", out var ti) ? ti.GetString() ?? string.Empty : string.Empty,
                        Score = hit.TryGetProperty("_score", out var sc) && sc.ValueKind == JsonValueKind.Number ? sc.GetDouble() : 0
                    };

                    if (hit.TryGetProperty("highlight", out var hl))
                    {
                        var snippet = string.Empty;
                        if (hl.TryGetProperty("body", out var bodyHl) && bodyHl.ValueKind == JsonValueKind.Array && bodyHl.GetArrayLength() > 0)
                        {
                            snippet = bodyHl[0].GetString() ?? string.Empty;
                        }
                        else if (hl.TryGetProperty("title", out var titleHl) && titleHl.ValueKind == JsonValueKind.Array && titleHl.GetArrayLength() > 0)
                        {
                            snippet = titleHl[0].GetString() ?? string.Empty;
                        }
                        h.Snippet = snippet;
                    }

                    if (source.TryGetProperty("meta", out var metaEl) && metaEl.ValueKind == JsonValueKind.Object)
                    {
                        var meta = new Dictionary<string, string?>();
                        foreach (var prop in metaEl.EnumerateObject())
                        {
                            meta[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
                        }
                        h.Meta = meta;
                    }

                    hits.Add(h);
                }
            }
        }
        return new SearchQueryResponse { Hits = hits, TotalCandidates = total };
    }

    public async Task<long> GetDocumentCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_options.IndexName}/_count", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return 0;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("count", out var countEl) && countEl.TryGetInt64(out var count)
                ? count
                : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nelze zjistit počet dokumentů v OpenSearch indexu.");
            return 0;
        }
    }

    public async Task<bool> IsSearchableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, _options.IndexName),
                cancellationToken).ConfigureAwait(false);
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nelze ověřit dostupnost OpenSearch indexu.");
            return false;
        }
    }
}
