using System.Text;
using System.Text.Json;
using UnlockedAI.Core.Search;

namespace UnlockedAI.Core.Tools.Builtin;

public sealed class WebSearchTool(SearchService search) : ToolBase
{
    // Fixed, not chosen by the model: left to choose, it asks for one result, and one snippet
    // rarely holds the answer.
    private const int ResultCount = 5;

    public override string Name => "web_search";

    public override string Title => "Web search";

    public override string Summary => "Search the web and read the result titles and snippets.";

    // Kept short on purpose: every tool's description is sent with every message, and small models
    // make worse tool calls the more of this text there is.
    public override string Description =>
        "Search the web for current or uncertain information. Returns numbered results with title, address and snippet. "
        + "Pass an address to web_fetch to read it in full.";

    public override string ParametersJson =>
        """
        {
          "type": "object",
          "required": ["query"],
          "properties": {
            "query": { "type": "string", "description": "What to search for." }
          }
        }
        """;

    public override string Describe(JsonElement arguments) => Text(arguments, "query");

    protected override async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var query = RequiredText(arguments, "query");

        var provider = search.Current;
        var results = await provider.SearchAsync(query, ResultCount, cancellationToken).ConfigureAwait(false);
        if (results.Count == 0)
        {
            return $"No results for \"{query}\" from {provider.Name}.";
        }

        var text = new StringBuilder();
        text.Append("Results from ").Append(provider.Name).Append(" for \"").Append(query).AppendLine("\":");
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            text.AppendLine();
            text.Append(index + 1).Append(". ").AppendLine(result.Title);
            text.Append("   ").AppendLine(result.Url);
            if (result.Snippet.Length > 0)
            {
                text.Append("   ").AppendLine(result.Snippet);
            }
        }

        return text.ToString();
    }
}
