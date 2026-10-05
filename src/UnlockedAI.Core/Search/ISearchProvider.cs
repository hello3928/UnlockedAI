namespace UnlockedAI.Core.Search;

public sealed record SearchResult(string Title, string Url, string Snippet);

/// <summary>One web search backend. Adding a backend means adding one of these.</summary>
public interface ISearchProvider
{
    /// <summary>Shown to the model with the results, so it can say where they came from.</summary>
    string Name { get; }

    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken);
}
