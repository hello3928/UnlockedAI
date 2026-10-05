using System.Buffers;
using System.Net;
using System.Text;
using UnlockedAI.Core.Tools;

namespace UnlockedAI.Core.Web;

/// <param name="Text">The body decoded as text, cut at <see cref="WebReader.MaxBytes"/>. Empty for non-text content.</param>
public sealed record WebResponse(
    Uri FinalUri,
    int Status,
    string Reason,
    string ContentType,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    string Text,
    bool Truncated)
{
    public bool IsText =>
        ContentType.Length == 0
        || ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || ContentType.Contains("json", StringComparison.OrdinalIgnoreCase)
        || ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase)
        || ContentType.Contains("javascript", StringComparison.OrdinalIgnoreCase);

    public bool IsHtml => ContentType.Contains("html", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The one HTTP client the tools share. It never buffers more than <see cref="MaxBytes"/> of a response,
/// and it follows redirects itself so a public page can't bounce a request to a local address.
/// </summary>
public sealed class WebReader : IDisposable
{
    public const int MaxBytes = 2 * 1024 * 1024;

    private const int MaxRedirects = 5;
    private const int ChunkBytes = 64 * 1024;

    // Many sites refuse clients that don't look like a browser.
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private readonly HttpClient _http;

    /// <param name="handler">Replaces the network layer. Used by tests.</param>
    public WebReader(HttpMessageHandler? handler = null)
    {
        _http = new HttpClient(handler ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    /// <summary>Parses an address the model supplied. Only http and https are allowed.</summary>
    public static Uri ParseUrl(string url)
    {
        var text = url.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : throw new ToolException($"\"{url}\" is not a valid http or https address.");
    }

    public async Task<WebResponse> SendAsync(
        HttpMethod method,
        Uri uri,
        IReadOnlyDictionary<string, string>? headers,
        string? body,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        try
        {
            for (var hop = 0; ; hop++)
            {
                using var request = BuildRequest(method, uri, headers, body);
                using var response = await _http
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                    .ConfigureAwait(false);

                if (RedirectTarget(response, uri) is not { } next)
                {
                    return await ReadAsync(uri, response, deadline.Token).ConfigureAwait(false);
                }

                if (hop >= MaxRedirects)
                {
                    throw new ToolException("The address redirected too many times.");
                }

                if (LocalAddress.IsLocal(next) && !LocalAddress.IsLocal(uri))
                {
                    throw new ToolException($"The address redirected to a local address ({next.Host}), which was not followed.");
                }

                // As browsers do: a redirected POST becomes a GET, except for 307 and 308 which keep the method.
                if (response.StatusCode is not (HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                    && method != HttpMethod.Head)
                {
                    method = HttpMethod.Get;
                    body = null;
                }

                uri = next;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ToolException($"No answer from {uri.Host} within {timeout.TotalSeconds:0} seconds.");
        }
        catch (HttpRequestException exception)
        {
            throw new ToolException($"Couldn't reach {uri.Host}: {exception.Message}");
        }
    }

    public void Dispose() => _http.Dispose();

    private static HttpRequestMessage BuildRequest(
        HttpMethod method,
        Uri uri,
        IReadOnlyDictionary<string, string>? headers,
        string? body)
    {
        var request = new HttpRequestMessage(method, uri);

        string? contentType = null;
        var hasUserAgent = false;
        if (headers is not null)
        {
            foreach (var (name, value) in headers)
            {
                if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = value;
                }
                else if (request.Headers.TryAddWithoutValidation(name, value))
                {
                    // Headers the request can't carry (such as Content-Length) are quietly left out.
                    hasUserAgent |= name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        if (!hasUserAgent)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.Remove("Content-Type");
            request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType ?? GuessContentType(body));
        }

        return request;
    }

    private static string GuessContentType(string body) =>
        body.AsSpan().TrimStart() is ['{' or '[', ..] ? "application/json" : "text/plain; charset=utf-8";

    private static Uri? RedirectTarget(HttpResponseMessage response, Uri current) =>
        (int)response.StatusCode is 301 or 302 or 303 or 307 or 308 && response.Headers.Location is { } location
            ? (location.IsAbsoluteUri ? location : new Uri(current, location))
            : null;

    private static async Task<WebResponse> ReadAsync(Uri uri, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        var headers = response.Headers.Concat(response.Content.Headers)
            .Select(header => KeyValuePair.Create(header.Key, string.Join(", ", header.Value)))
            .ToList();

        var result = new WebResponse(uri, (int)response.StatusCode, response.ReasonPhrase ?? "", contentType, headers, "", false);
        if (!result.IsText)
        {
            return result;
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            // Grows with the page, so a typical small page costs a small buffer; stops at the limit.
            using var body = new MemoryStream();
            var chunk = ArrayPool<byte>.Shared.Rent(ChunkBytes);
            var truncated = false;
            try
            {
                while (true)
                {
                    var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    var room = MaxBytes - (int)body.Length;
                    if (read > room)
                    {
                        body.Write(chunk, 0, room);
                        truncated = true;
                        break;
                    }

                    body.Write(chunk, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(chunk);
            }

            var encoding = EncodingFor(response.Content.Headers.ContentType?.CharSet);
            var text = encoding.GetString(body.GetBuffer(), 0, (int)body.Length);
            return result with { Text = text, Truncated = truncated };
        }
    }

    private static Encoding EncodingFor(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }
}
