using System.Net;
using System.Text.Json;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Errors;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Ollama;
using UnlockedAI.Tests.Support;

namespace UnlockedAI.Tests.Ollama;

public sealed class OllamaClientTests : IDisposable
{
    private static readonly ModelOptions Options = new("test-model", Temperature: 0.5, ContextLength: 8192, KeepAliveMinutes: 5);
    private static readonly ChatTurn[] Hello = [new(ChatRole.User, "hello")];

    private readonly Database _database = Database.OpenInMemory();
    private readonly SettingsService _settings;

    public OllamaClientTests() => _settings = new SettingsService(new SettingsRepository(_database));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Stream_yields_text_then_tool_call_then_done()
    {
        var handler = StubHttpHandler.Json(
            """
            {"message":{"role":"assistant","content":"Hel"},"done":false}
            {"message":{"role":"assistant","content":"lo"},"done":false}
            {"message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"web_search","arguments":{"query":"cats"}}}]},"done":false}
            {"message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","prompt_eval_count":12,"eval_count":7}
            """);
        using var client = new OllamaClient(_settings, handler);

        var events = await client.StreamChatAsync(Options, Hello, [], Ct).ToListAsync(Ct);

        Assert.Equal(
            [
                new ModelText("Hel"),
                new ModelText("lo"),
                new ModelToolCall(new ToolCall("web_search", """{"query":"cats"}""")),
                new ModelDone(12, 7),
            ],
            events);
    }

    [Fact]
    public async Task Reply_that_ran_into_the_length_limit_is_flagged()
    {
        var handler = StubHttpHandler.Json("""{"message":{"role":"assistant","content":"x"},"done":true,"done_reason":"length"}""");
        using var client = new OllamaClient(_settings, handler);

        var events = await client.StreamChatAsync(Options, Hello, [], Ct).ToListAsync(Ct);

        Assert.True(Assert.IsType<ModelDone>(events[^1]).HitLengthLimit);
    }

    [Fact]
    public async Task Request_carries_model_options_and_omits_tools_when_there_are_none()
    {
        var handler = StubHttpHandler.Json("""{"done":true}""");
        using var client = new OllamaClient(_settings, handler);

        await client.StreamChatAsync(Options, Hello, [], Ct).ToListAsync(Ct);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal("http://localhost:11434/api/chat", handler.LastRequestUri!.ToString());
        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.Equal("5m", root.GetProperty("keep_alive").GetString());
        Assert.Equal(8192, root.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal(OllamaClient.MaxReplyTokens, root.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.Equal(0.5, root.GetProperty("options").GetProperty("temperature").GetDouble());
        Assert.Equal("hello", root.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.False(root.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task Request_includes_tools_and_earlier_tool_messages()
    {
        var handler = StubHttpHandler.Json("""{"done":true}""");
        using var client = new OllamaClient(_settings, handler);
        ChatTurn[] turns =
        [
            new(ChatRole.Assistant, "", ToolCalls: [new ToolCall("system_info", "{}")]),
            new(ChatRole.Tool, "Windows 11", ToolName: "system_info"),
        ];
        ToolDefinition[] tools = [new("system_info", "Reports the OS.", """{"type":"object","properties":{}}""")];

        await client.StreamChatAsync(Options, turns, tools, Ct).ToListAsync(Ct);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        var function = root.GetProperty("tools")[0].GetProperty("function");
        Assert.Equal("system_info", function.GetProperty("name").GetString());
        Assert.Equal("object", function.GetProperty("parameters").GetProperty("type").GetString());

        var messages = root.GetProperty("messages");
        Assert.Equal("system_info", messages[0].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Object, messages[0].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("arguments").ValueKind);
        Assert.Equal("tool", messages[1].GetProperty("role").GetString());
        Assert.Equal("system_info", messages[1].GetProperty("tool_name").GetString());
    }

    [Fact]
    public async Task Unknown_model_becomes_model_not_found()
    {
        var handler = StubHttpHandler.Json("""{"error":"model 'test-model' not found"}""", HttpStatusCode.NotFound);
        using var client = new OllamaClient(_settings, handler);

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await client.StreamChatAsync(Options, Hello, [], Ct).ToListAsync(Ct));

        Assert.Equal(AppErrorKind.ModelNotFound, exception.Error.Kind);
    }

    [Fact]
    public async Task Refused_connection_becomes_a_retryable_unreachable_error()
    {
        var handler = new StubHttpHandler(_ => throw new HttpRequestException("connection refused"));
        using var client = new OllamaClient(_settings, handler);

        var exception = await Assert.ThrowsAsync<AppException>(() => client.ListModelsAsync(Ct));

        Assert.Equal(AppErrorKind.OllamaUnreachable, exception.Error.Kind);
        Assert.True(exception.Error.IsRetryable);
        Assert.Contains("http://localhost:11434", exception.Error.Message);
    }

    [Fact]
    public async Task Error_line_inside_a_stream_is_raised()
    {
        var handler = StubHttpHandler.Json(
            """
            {"message":{"role":"assistant","content":"partial"},"done":false}
            {"error":"out of memory"}
            """);
        using var client = new OllamaClient(_settings, handler);

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await client.StreamChatAsync(Options, Hello, [], Ct).ToListAsync(Ct));

        Assert.Equal("out of memory", exception.Error.Detail);
    }

    [Fact]
    public async Task Models_are_listed_by_name_with_their_tool_support()
    {
        var handler = StubHttpHandler.Json(
            """
            {"models":[
              {"name":"zeta:latest","size":200,"details":{"parameter_size":"8B"},"capabilities":["completion"]},
              {"name":"Alpha:latest","size":100,"details":{"parameter_size":"8.0B"},"capabilities":["completion","tools"]},
              {"name":"old:latest","size":50}
            ]}
            """);
        using var client = new OllamaClient(_settings, handler);

        var models = await client.ListModelsAsync(Ct);

        Assert.Equal(["Alpha:latest", "old:latest", "zeta:latest"], models.Select(model => model.Name));
        Assert.True(models[0].SupportsTools);
        Assert.False(models[1].SupportsTools);
        Assert.False(models[2].SupportsTools);
        Assert.Equal("8.0B", models[0].ParameterSize);
    }

    [Fact]
    public async Task Invalid_address_in_settings_is_reported_as_invalid_input()
    {
        await _settings.SaveAsync(new AppSettings { OllamaUrl = "not a url" }, Ct);
        using var client = new OllamaClient(_settings, StubHttpHandler.Json("{}"));

        var exception = await Assert.ThrowsAsync<AppException>(() => client.ListModelsAsync(Ct));

        Assert.Equal(AppErrorKind.InvalidInput, exception.Error.Kind);
    }
}
