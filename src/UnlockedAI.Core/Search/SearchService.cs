namespace UnlockedAI.Core.Search;

/// <summary>
/// Picks the search backend: Ollama Web Search once the user has saved a key for it,
/// DuckDuckGo otherwise so that search works with no setup.
/// </summary>
public sealed class SearchService(DuckDuckGoSearch duckDuckGo, OllamaWebSearch ollama)
{
    public ISearchProvider Current => ollama.IsConfigured ? ollama : duckDuckGo;
}
