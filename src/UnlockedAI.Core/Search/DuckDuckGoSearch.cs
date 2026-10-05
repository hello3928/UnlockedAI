using System.Web;
using AngleSharp.Html.Parser;
using UnlockedAI.Core.Tools;
using UnlockedAI.Core.Web;

namespace UnlockedAI.Core.Search;

/// <summary>
/// Searches through DuckDuckGo's plain HTML results page. It needs no account, which makes it the
/// default, but it reads a web page rather than an API, so it can be rate-limited or change shape.
/// </summary>
public sealed class DuckDuckGoSearch(WebReader web) : ISearchProvider
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public string Name => "DuckDuckGo";

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        var uri = new Uri("https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query));
        var response = await web.SendAsync(HttpMethod.Get, uri, headers: null, body: null, Timeout, cancellationToken).ConfigureAwait(false);

        if (response.Status != 200)
        {
            throw new ToolException(
                $"DuckDuckGo answered with status {response.Status}. It may be limiting requests; try again in a minute, "
                + "or add an Ollama API key in Settings to use Ollama Web Search instead.");
        }

        var results = Parse(response.Text, maxResults);
        if (results.Count == 0 && response.Text.Contains("anomaly", StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolException(
                "DuckDuckGo is asking for a human check, so no results came back. Try again later, "
                + "or add an Ollama API key in Settings to use Ollama Web Search instead.");
        }

        return results;
    }

    internal static List<SearchResult> Parse(string html, int maxResults)
    {
        using var document = new HtmlParser().ParseDocument(html);
        var results = new List<SearchResult>(maxResults);

        foreach (var result in document.QuerySelectorAll(".result:not(.result--ad)"))
        {
            if (result.QuerySelector("a.result__a") is not { } link
                || TargetOf(link.GetAttribute("href")) is not { } url)
            {
                continue;
            }

            var snippet = result.QuerySelector(".result__snippet")?.TextContent.Trim() ?? "";
            results.Add(new SearchResult(link.TextContent.Trim(), url, snippet));

            if (results.Count == maxResults)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>Result links go through a DuckDuckGo redirect that carries the real address in its "uddg" parameter.</summary>
    private static string? TargetOf(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var absolute = href.StartsWith("//", StringComparison.Ordinal) ? "https:" + href : href;
        if (!Uri.TryCreate(absolute, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Host.EndsWith("duckduckgo.com", StringComparison.OrdinalIgnoreCase)
            && HttpUtility.ParseQueryString(uri.Query)["uddg"] is { Length: > 0 } target)
        {
            return target;
        }

        return uri.AbsoluteUri;
    }
}
