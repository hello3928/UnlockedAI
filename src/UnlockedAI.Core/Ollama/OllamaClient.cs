using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Errors;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Ollama;

/// <summary>
/// Talks to a local Ollama server. One instance, and so one <see cref="HttpClient"/>, lives for the whole app.
/// Every failure surfaces as an <see cref="AppException"/>.
/// </summary>
public sealed class OllamaClient : IOllamaClient, IDisposable
{
    private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly SettingsService _settings;

    /// <param name="handler">Replaces the network layer. Used by tests.</param>
    public OllamaClient(SettingsService settings, HttpMessageHandler? handler = null)
    {
        _settings = settings;
        _http = new HttpClient(handler ?? new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) })
        {
            // A reply can stream for minutes. Callers cancel; the client never gives up on its own.
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    private string BaseUrl => _settings.Current.OllamaUrl;

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ListTimeout);

        try
        {
            using var response = await _http
                .GetAsync(Endpoint("api/tags"), HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            await EnsureSuccessAsync(response, model: null, timeout.Token).ConfigureAwait(false);

            var tags = await response.Content
                .ReadFromJsonAsync(OllamaJsonContext.Default.TagsDto, timeout.Token)
                .ConfigureAwait(false);

            var models = new List<ModelInfo>();
            foreach (var model in tags?.Models ?? [])
            {
                models.Add(new ModelInfo(
                    model.Name,
                    model.Size,
                    model.Capabilities?.Contains("tools") ?? false,
                    model.Details?.ParameterSize));
            }

            models.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return models;
        }
        catch (HttpRequestException exception)
        {
            throw Unreachable(exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own timeout fired, not the caller's cancellation.
            throw Unreachable(exception);
        }
        catch (JsonException exception)
        {
            throw new AppException(AppError.Network($"Unexpected reply from Ollama: {exception.Message}"), exception);
        }
    }

    public async IAsyncEnumerable<ModelEvent> StreamChatAsync(
        ModelOptions options,
        IReadOnlyList<ChatTurn> turns,
        IReadOnlyList<ToolDefinition> tools,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint("api/chat"))
        {
            // Serialised straight to the socket; the request body is never held as one string.
            Content = JsonContent.Create(BuildRequest(options, turns, tools), OllamaJsonContext.Default.ChatRequestDto),
        };

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, options.Model, cancellationToken).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var chunks = Ndjson
                .ReadAsync(stream, OllamaJsonContext.Default.ChatChunkDto, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
            await using (chunks.ConfigureAwait(false))
            {
                while (await MoveNextAsync(chunks).ConfigureAwait(false))
                {
                    var chunk = chunks.Current;
                    if (chunk.Error is { } error)
                    {
                        throw new AppException(AppError.Network(error));
                    }

                    if (chunk.Message is { } message)
                    {
                        if (message.Content.Length > 0)
                        {
                            yield return new ModelText(message.Content);
                        }

                        foreach (var call in message.ToolCalls ?? [])
                        {
                            if (call.Function is { Name.Length: > 0 } function)
                            {
                                yield return new ModelToolCall(new ToolCall(function.Name, ArgumentsText(function.Arguments)));
                            }
                        }
                    }

                    if (chunk.Done)
                    {
                        yield return new ModelDone(chunk.PromptEvalCount ?? 0, chunk.EvalCount ?? 0);
                    }
                }
            }
        }
    }

    public void Dispose() => _http.Dispose();

    private static ChatRequestDto BuildRequest(
        ModelOptions options,
        IReadOnlyList<ChatTurn> turns,
        IReadOnlyList<ToolDefinition> tools)
    {
        var messages = new List<MessageDto>(turns.Count);
        foreach (var turn in turns)
        {
            messages.Add(new MessageDto
            {
                Role = turn.Role.ToWire(),
                Content = turn.Content,
                ToolName = turn.ToolName,
                ToolCalls = turn.ToolCalls is { Count: > 0 } calls ? calls.Select(ToDto).ToList() : null,
            });
        }

        return new ChatRequestDto
        {
            Model = options.Model,
            Messages = messages,
            Tools = tools.Count > 0 ? tools.Select(ToDto).ToList() : null,
            Options = new OptionsDto { Temperature = options.Temperature, NumCtx = options.ContextLength },
            KeepAlive = string.Create(CultureInfo.InvariantCulture, $"{options.KeepAliveMinutes}m"),
        };
    }

    private static ToolCallDto ToDto(ToolCall call) => new()
    {
        Function = new FunctionCallDto { Name = call.Name, Arguments = ParseObject(call.ArgumentsJson) },
    };

    private static ToolDto ToDto(ToolDefinition tool) => new()
    {
        Function = new FunctionDto
        {
            Name = tool.Name,
            Description = tool.Description,
            Parameters = ParseObject(tool.ParametersJson),
        },
    };

    private static JsonElement ParseObject(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    private static string ArgumentsText(JsonElement arguments) =>
        arguments.ValueKind == JsonValueKind.Undefined ? "{}" : arguments.GetRawText();

    private Uri Endpoint(string path)
    {
        if (Uri.TryCreate(BaseUrl.TrimEnd('/') + "/" + path, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri;
        }

        throw new AppException(AppError.InvalidInput(
            $"The Ollama address \"{BaseUrl}\" isn't a valid URL. Fix it in Settings."));
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw Unreachable(exception);
        }
    }

    private async ValueTask<bool> MoveNextAsync(IAsyncEnumerator<ChatChunkDto> chunks)
    {
        try
        {
            return await chunks.MoveNextAsync().ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            // The connection dropped mid-reply, usually because Ollama was closed.
            throw Unreachable(exception);
        }
        catch (JsonException exception)
        {
            throw new AppException(AppError.Network($"Unexpected reply from Ollama: {exception.Message}"), exception);
        }
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string? model, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? detail = null;
        try
        {
            var body = await response.Content
                .ReadFromJsonAsync(OllamaJsonContext.Default.ErrorDto, cancellationToken)
                .ConfigureAwait(false);
            detail = body?.Error;
        }
        catch (JsonException)
        {
            // Not a JSON error body; the status code alone will have to do.
        }

        if (response.StatusCode == HttpStatusCode.NotFound && model is not null)
        {
            throw new AppException(AppError.ModelNotFound(model));
        }

        throw new AppException(new AppError(
            AppErrorKind.Network,
            "Ollama refused the request",
            detail ?? $"Ollama answered with status {(int)response.StatusCode}.",
            detail,
            IsRetryable: true));
    }

    private AppException Unreachable(Exception inner) =>
        new(AppError.OllamaUnreachable(BaseUrl, inner.Message), inner);
}
