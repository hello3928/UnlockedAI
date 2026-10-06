using System.Text;
using System.Text.Json;
using UnlockedAI.Core.Web;

namespace UnlockedAI.Core.Tools.Builtin;

public sealed class HttpRequestTool(WebReader web) : ToolBase
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly string[] Methods = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD"];

    public override string Name => "http_request";

    public override string Title => "Web request";

    public override string Summary => "Send an HTTP request to an API and read the raw response.";

    public override string Description =>
        "Send an HTTP request and return the status, headers and body as received. For APIs and JSON; use web_fetch for ordinary pages.";

    public override string ParametersJson =>
        """
        {
          "type": "object",
          "required": ["url"],
          "properties": {
            "url": { "type": "string", "description": "Full address, starting with https://" },
            "method": { "type": "string", "description": "GET (default), POST, PUT, PATCH, DELETE or HEAD." },
            "headers": { "type": "object", "description": "Header names and values." },
            "body": { "type": "string", "description": "Request body." }
          }
        }
        """;

    // Waits for approval when the request sends data out or acts on another system: any method other
    // than GET or HEAD, or any request with a body. A request to this PC or the local network also
    // waits, since it can drive a router or a local service. A plain read of a public URL does not.
    public override ToolRisk RiskFor(JsonElement arguments)
    {
        var method = MethodOf(arguments);
        if (method is not ("GET" or "HEAD") || Text(arguments, "body").Length > 0)
        {
            return ToolRisk.ChangesPc;
        }

        return Uri.TryCreate(Text(arguments, "url"), UriKind.Absolute, out var uri) && LocalAddress.IsLocal(uri)
            ? ToolRisk.ChangesPc
            : ToolRisk.ReadOnly;
    }

    // Shows the body as well, so an approval card reveals exactly what would be sent.
    public override string Describe(JsonElement arguments)
    {
        var line = $"{MethodOf(arguments)} {Text(arguments, "url")}";
        var body = Text(arguments, "body");
        return body.Length > 0 ? $"{line}\n{body}" : line;
    }

    protected override async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var uri = WebReader.ParseUrl(RequiredText(arguments, "url"));
        var method = MethodOf(arguments);
        if (!Methods.Contains(method))
        {
            throw new ToolException($"\"{method}\" is not a supported method. Use one of: {string.Join(", ", Methods)}.");
        }

        var body = Text(arguments, "body");
        var response = await web
            .SendAsync(new HttpMethod(method), uri, HeadersOf(arguments), body.Length > 0 ? body : null, Timeout, cancellationToken)
            .ConfigureAwait(false);

        var text = new StringBuilder();
        text.Append("HTTP ").Append(response.Status).Append(' ').AppendLine(response.Reason);
        foreach (var (name, value) in response.Headers)
        {
            text.Append(name).Append(": ").AppendLine(value);
        }

        text.AppendLine();
        text.Append(response.IsText ? response.Text : $"[{response.ContentType} content, not shown]");
        if (response.Truncated)
        {
            text.AppendLine().Append("[Body cut at 2 MB.]");
        }

        return text.ToString();
    }

    private static string MethodOf(JsonElement arguments)
    {
        var method = Text(arguments, "method").Trim().ToUpperInvariant();
        return method.Length > 0 ? method : "GET";
    }

    private static Dictionary<string, string>? HeadersOf(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("headers", out var headers)
            || headers.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers.EnumerateObject())
        {
            result[header.Name] = header.Value.ValueKind == JsonValueKind.String
                ? header.Value.GetString() ?? ""
                : header.Value.GetRawText();
        }

        return result;
    }
}
