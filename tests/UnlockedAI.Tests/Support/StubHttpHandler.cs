using System.Net;
using System.Text;

namespace UnlockedAI.Tests.Support;

/// <summary>Answers HTTP requests from a function and remembers the last request body.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public string? LastRequestBody { get; private set; }

    public Uri? LastRequestUri { get; private set; }

    public static StubHttpHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return respond(request);
    }
}
