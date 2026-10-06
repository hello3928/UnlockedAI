using System.Net;
using System.Text;
using System.Text.Json;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Search;
using UnlockedAI.Core.Secrets;
using UnlockedAI.Core.Tools;
using UnlockedAI.Core.Tools.Builtin;
using UnlockedAI.Core.Web;
using UnlockedAI.Tests.Support;

namespace UnlockedAI.Tests.Tools;

public class WebToolTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("http://localhost:11434/api/tags", true)]
    [InlineData("http://127.0.0.1/", true)]
    [InlineData("http://192.168.1.1/admin", true)]
    [InlineData("http://10.0.0.5/", true)]
    [InlineData("http://172.20.3.4/", true)]
    [InlineData("http://169.254.169.254/latest/meta-data", true)]
    [InlineData("http://[::1]/", true)]
    [InlineData("http://router/", true)]
    [InlineData("http://printer.local/", true)]
    [InlineData("https://example.com/", false)]
    [InlineData("https://172.32.0.1/", false)]
    [InlineData("https://8.8.8.8/", false)]
    public void Local_addresses_are_told_apart_from_public_ones(string url, bool expectedLocal)
    {
        Assert.Equal(expectedLocal, LocalAddress.IsLocal(new Uri(url)));
    }

    [Fact]
    public void Requests_to_local_addresses_need_approval_and_public_ones_do_not()
    {
        using var web = new WebReader(StubHttpHandler.Json("{}"));
        ITool[] tools = [new WebFetchTool(web), new HttpRequestTool(web)];

        foreach (var tool in tools)
        {
            Assert.Equal(ToolRisk.ChangesPc, tool.RiskFor(Args(new { url = "http://localhost:8080/" })));
            Assert.Equal(ToolRisk.ReadOnly, tool.RiskFor(Args(new { url = "https://example.com/" })));
        }
    }

    [Fact]
    public void Web_requests_that_send_data_out_need_approval_even_to_a_public_host()
    {
        using var web = new WebReader(StubHttpHandler.Json("{}"));
        var tool = new HttpRequestTool(web);

        Assert.Equal(ToolRisk.ReadOnly, tool.RiskFor(Args(new { url = "https://api.example.com/items" })));
        Assert.Equal(ToolRisk.ChangesPc, tool.RiskFor(Args(new { url = "https://api.example.com/items", method = "POST", body = "x" })));
        Assert.Equal(ToolRisk.ChangesPc, tool.RiskFor(Args(new { url = "https://api.example.com/items", method = "delete" })));
        // A GET that carries a body is still sending data out.
        Assert.Equal(ToolRisk.ChangesPc, tool.RiskFor(Args(new { url = "https://api.example.com/items", body = "payload" })));
    }

    [Fact]
    public void A_request_with_a_body_shows_the_body_on_its_approval_card()
    {
        using var web = new WebReader(StubHttpHandler.Json("{}"));
        var summary = new HttpRequestTool(web).Describe(Args(new { url = "https://api.example.com/", method = "POST", body = "name=pen" }));

        Assert.Contains("POST https://api.example.com/", summary);
        Assert.Contains("name=pen", summary);
    }

    [Fact]
    public async Task Page_is_reduced_to_its_readable_text()
    {
        using var web = new WebReader(Html(
            """
            <html><head><title>  Growing   tomatoes </title><style>p { color: red }</style></head>
            <body>
              <nav><a href="/">Home</a> <a href="/shop">Shop</a></nav>
              <main>
                <h1>Growing tomatoes</h1>
                <p>Plant them   in <b>full sun</b>.</p>
                <ul><li>Water daily</li><li>Feed weekly</li></ul>
                <script>track()</script>
              </main>
              <footer>Copyright</footer>
            </body></html>
            """));

        var result = await new WebFetchTool(web).RunAsync(Args(new { url = "https://example.com/tomatoes" }), Ct);

        Assert.Equal(
            """
            Growing tomatoes
            https://example.com/tomatoes

            # Growing tomatoes
            Plant them in full sun.
            - Water daily
            - Feed weekly
            """.ReplaceLineEndings("\n"),
            result.Output);
    }

    [Fact]
    public async Task Fetch_refuses_files_that_are_not_text_and_reports_error_pages()
    {
        using var image = new WebReader(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new("image/png") } },
        }));
        using var missing = new WebReader(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var imageResult = await new WebFetchTool(image).RunAsync(Args(new { url = "https://example.com/a.png" }), Ct);
        var missingResult = await new WebFetchTool(missing).RunAsync(Args(new { url = "https://example.com/gone" }), Ct);

        Assert.Equal(ToolOutcome.Failed, imageResult.Outcome);
        Assert.Contains("image/png", imageResult.Output);
        Assert.Contains("status 404", missingResult.Output);
    }

    [Fact]
    public async Task Redirect_from_a_public_page_to_a_local_address_is_not_followed()
    {
        using var web = new WebReader(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("http://192.168.1.1/admin") },
        }));

        var result = await new WebFetchTool(web).RunAsync(Args(new { url = "https://example.com/" }), Ct);

        Assert.Equal(ToolOutcome.Failed, result.Outcome);
        Assert.Contains("redirected to a local address", result.Output);
    }

    [Fact]
    public async Task Ordinary_redirects_are_followed()
    {
        var handler = new StubHttpHandler(request => request.RequestUri!.AbsolutePath == "/old"
            ? new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Headers = { Location = new Uri("/new", UriKind.Relative) } }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("arrived", Encoding.UTF8, "text/plain") });
        using var web = new WebReader(handler);

        var result = await new WebFetchTool(web).RunAsync(Args(new { url = "https://example.com/old" }), Ct);

        Assert.Equal("https://example.com/new\n\narrived", result.Output);
    }

    [Fact]
    public async Task Response_larger_than_the_limit_is_cut()
    {
        var large = new string('a', WebReader.MaxBytes + 1000);
        using var web = new WebReader(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(large, Encoding.UTF8, "text/plain"),
        }));

        var response = await web.SendAsync(HttpMethod.Get, new Uri("https://example.com/big"), null, null, TimeSpan.FromSeconds(5), Ct);

        Assert.True(response.Truncated);
        Assert.Equal(WebReader.MaxBytes, response.Text.Length);
    }

    [Fact]
    public async Task Http_request_sends_method_headers_and_body_and_returns_the_raw_response()
    {
        HttpRequestMessage? seen = null;
        var handler = new StubHttpHandler(request =>
        {
            seen = request;
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"id":7}""", Encoding.UTF8, "application/json"),
            };
        });
        using var web = new WebReader(handler);

        var result = await new HttpRequestTool(web).RunAsync(
            Args(new
            {
                url = "https://api.example.com/items",
                method = "post",
                headers = new Dictionary<string, string> { ["X-Token"] = "abc" },
                body = """{"name":"pen"}""",
            }),
            Ct);

        Assert.Equal(HttpMethod.Post, seen!.Method);
        Assert.Equal("abc", seen.Headers.GetValues("X-Token").Single());
        Assert.Equal("""{"name":"pen"}""", handler.LastRequestBody);
        Assert.StartsWith("HTTP 201 Created", result.Output);
        Assert.EndsWith("""{"id":7}""", result.Output);
    }

    [Fact]
    public async Task Http_request_rejects_addresses_that_are_not_web_addresses()
    {
        using var web = new WebReader(StubHttpHandler.Json("{}"));

        var result = await new HttpRequestTool(web).RunAsync(Args(new { url = "file:///C:/Windows/win.ini" }), Ct);

        Assert.Equal(ToolOutcome.Failed, result.Outcome);
        Assert.Contains("not a valid http or https address", result.Output);
    }

    [Fact]
    public void DuckDuckGo_results_are_read_from_its_html_and_adverts_are_skipped()
    {
        const string page =
            """
            <div class="result results_links result--ad"><a class="result__a" href="https://ads.example/x">Advert</a></div>
            <div class="result results_links web-result">
              <h2><a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fone%3Fa%3D1&amp;rut=abc"> First result </a></h2>
              <a class="result__snippet" href="#">About the <b>first</b> thing.</a>
            </div>
            <div class="result results_links web-result">
              <h2><a class="result__a" href="https://example.org/two">Second result</a></h2>
            </div>
            """;

        var results = DuckDuckGoSearch.Parse(page, maxResults: 5);

        Assert.Equal(
            [
                new SearchResult("First result", "https://example.com/one?a=1", "About the first thing."),
                new SearchResult("Second result", "https://example.org/two", ""),
            ],
            results);
        Assert.Single(DuckDuckGoSearch.Parse(page, maxResults: 1));
    }

    [Fact]
    public async Task Search_uses_duckduckgo_until_an_ollama_key_is_saved()
    {
        var secrets = new MemorySecretStore();
        HttpRequestMessage? seen = null;
        var handler = new StubHttpHandler(request =>
        {
            seen = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"results":[{"title":"Ollama","url":"https://ollama.com","content":"Run models locally."}]}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        using var web = new WebReader(handler);
        var service = new SearchService(new DuckDuckGoSearch(web), new OllamaWebSearch(web, secrets));
        var tool = new WebSearchTool(service);

        Assert.Equal("DuckDuckGo", service.Current.Name);

        secrets.Write(SecretNames.OllamaApiKey, "secret-key");
        var result = await tool.RunAsync(Args(new { query = "ollama" }), Ct);

        Assert.Equal("Ollama Web Search", service.Current.Name);
        Assert.Equal("https://ollama.com/api/web_search", seen!.RequestUri!.ToString());
        Assert.Equal("Bearer secret-key", seen.Headers.GetValues("Authorization").Single());
        Assert.Equal("""{"query":"ollama","max_results":5}""", handler.LastRequestBody);
        Assert.Contains("1. Ollama\n   https://ollama.com\n   Run models locally.", result.Output.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Rejected_ollama_key_says_to_check_settings()
    {
        var secrets = new MemorySecretStore();
        secrets.Write(SecretNames.OllamaApiKey, "wrong");
        using var web = new WebReader(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var tool = new WebSearchTool(new SearchService(new DuckDuckGoSearch(web), new OllamaWebSearch(web, secrets)));

        var result = await tool.RunAsync(Args(new { query = "anything" }), Ct);

        Assert.Equal(ToolOutcome.Failed, result.Outcome);
        Assert.Contains("rejected the API key", result.Output);
    }

    [Fact]
    public async Task Long_tool_output_is_cut_before_it_reaches_the_model()
    {
        var tool = new FakeTool("big", ToolRisk.ReadOnly, new string('z', ToolBase.MaxOutputChars + 5000));

        var result = await tool.RunAsync(Args(new { }), Ct);

        Assert.StartsWith(new string('z', ToolBase.MaxOutputChars), result.Output);
        Assert.Contains("5,000 more characters were not shown", result.Output);
    }

    private static JsonElement Args(object arguments) => JsonSerializer.SerializeToElement(arguments);

    private static StubHttpHandler Html(string html) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") });
}
