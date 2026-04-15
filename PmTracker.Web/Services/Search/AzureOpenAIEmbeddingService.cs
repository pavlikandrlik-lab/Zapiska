using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PmTracker.Web.Services.Search;

public sealed class AzureOpenAIEmbeddingService : IEmbeddingService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _httpClient;
    private readonly SearchEmbeddingsOptions _options;
    private readonly ILogger<AzureOpenAIEmbeddingService> _logger;

    public AzureOpenAIEmbeddingService(HttpClient httpClient, IOptions<SearchOptions> options, ILogger<AzureOpenAIEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value.Embeddings;
        _logger = logger;

        if (!string.IsNullOrEmpty(_options.Endpoint))
        {
            _httpClient.BaseAddress = new Uri(_options.Endpoint.TrimEnd('/') + "/");
        }
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("api-key");
            _httpClient.DefaultRequestHeaders.Add("api-key", _options.ApiKey);
        }
    }

    public async Task<IReadOnlyList<float>?> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var payload = JsonSerializer.Serialize(new { input = text, model = _options.Model }, JsonOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var endpointPath = $"openai/deployments/{Uri.EscapeDataString(_options.Model)}/embeddings?api-version=2023-05-15";

        try
        {
            var response = await _httpClient.PostAsync(endpointPath, content, cancellationToken).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Embedding selhal: {Status} {Body}", (int)response.StatusCode, raw);
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            {
                return null;
            }

            var embedding = data[0].GetProperty("embedding");
            var vector = new List<float>(embedding.GetArrayLength());
            foreach (var v in embedding.EnumerateArray())
            {
                vector.Add(v.GetSingle());
            }
            return vector;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chyba při získávání embeddingu.");
            return null;
        }
    }
}
