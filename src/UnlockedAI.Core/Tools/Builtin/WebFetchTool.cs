using System.Text.Json;
using UnlockedAI.Core.Web;

namespace UnlockedAI.Core.Tools.Builtin;

public sealed class WebFetchTool(WebReader web) : ToolBase
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public override string Name => "web_fetch";

    public override string Title => "Read web page";

    public override string Summary => "Open a web address and read the page as plain text.";

    public override string Description =>
        "Fetch a web page and return its readable text. Use it for articles and documentation, such as a web_search result.";

    public override string ParametersJson =>
        """
        {
          "type": "object",
          "required": ["url"],
          "properties": {
            "url": { "type": "string", "description": "Full address, starting with https://" }
          }
        }
        """;

    // A page on this PC or the local network is not public browsing, so it waits for approval.
    public override ToolRisk RiskFor(JsonElement arguments) =>
        Uri.TryCreate(Text(arguments, "url"), UriKind.Absolute, out var uri) && LocalAddress.IsLocal(uri)
            ? ToolRisk.ChangesPc
            : ToolRisk.ReadOnly;

    public override string Describe(JsonElement arguments) => Text(arguments, "url");

    protected override async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var uri = WebReader.ParseUrl(RequiredText(arguments, "url"));
        var response = await web.SendAsync(HttpMethod.Get, uri, headers: null, body: null, Timeout, cancellationToken).ConfigureAwait(false);

        if (response.Status >= 400)
        {
            throw new ToolException($"{response.FinalUri} answered with status {response.Status} {response.Reason}.");
        }

        if (!response.IsText)
        {
            throw new ToolException($"{response.FinalUri} is a {response.ContentType} file, not a page of text.");
        }

        if (!response.IsHtml)
        {
            return $"{response.FinalUri}\n\n{response.Text}";
        }

        var (title, text) = HtmlToText.Convert(response.Text);
        if (text.Length == 0)
        {
            throw new ToolException($"{response.FinalUri} has no readable text. It may need JavaScript to show its content.");
        }

        return $"{title}\n{response.FinalUri}\n\n{text}";
    }
}
