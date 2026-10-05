using System.Text.Json;
using System.Text.Json.Serialization;
using UnlockedAI.Core.Secrets;
using UnlockedAI.Core.Tools;
using UnlockedAI.Core.Web;

namespace UnlockedAI.Core.Search;

/// <summary>Searches through Ollama's hosted web search API. Needs the API key of a free ollama.com account.</summary>
public sealed class OllamaWebSearch(WebReader web, ISecretStore secrets) : ISearchProvider
{
    private static readonly Uri Endpoint = new("https://ollama.com/api/web_search");
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public string Name => "Ollama Web Search";

    /// <summary>True when a key has been saved, which is what switches searching over to this provider.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(secrets.Read(SecretNames.OllamaApiKey));

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        var key = secrets.Read(SecretNames.OllamaApiKey)
            ?? throw new ToolException("No Ollama API key is saved. Add one in Settings.");

        var body = JsonSerializer.Serialize(
            new SearchRequest { Query = query, MaxResults = Math.Clamp(maxResults, 1, 10) },
            SearchJsonContext.Default.SearchRequest);
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {key.Trim()}",
            ["Content-Type"] = "application/json",
        };

        var response = await web.SendAsync(HttpMethod.Post, Endpoint, headers, body, Timeout, cancellationToken).ConfigureAwait(false);

        if (response.Status is 401 or 403)
        {
            throw new ToolException("Ollama rejected the API key. Check the key in Settings.");
        }

        if (response.Status != 200)
        {
            throw new ToolException($"Ollama Web Search answered with status {response.Status}.");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize(response.Text, SearchJsonContext.Default.SearchResponse);
            return [.. (parsed?.Results ?? []).Select(result => new SearchResult(result.Title ?? "", result.Url ?? "", result.Content ?? ""))];
        }
        catch (JsonException exception)
        {
            throw new ToolException($"Ollama Web Search sent a reply that couldn't be read: {exception.Message}");
        }
    }
}

internal sealed class SearchRequest
{
    public string Query { get; init; } = "";
    public int MaxResults { get; init; }
}

internal sealed class SearchResponse
{
    public List<SearchResponseItem>? Results { get; init; }
}

internal sealed class SearchResponseItem
{
    public string? Title { get; init; }
    public string? Url { get; init; }
    public string? Content { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(SearchResponse))]
internal sealed partial class SearchJsonContext : JsonSerializerContext;
