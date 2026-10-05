using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Ollama;

/// <summary>An installed model as the model picker shows it.</summary>
public sealed record ModelInfo(string Name, long SizeBytes, bool SupportsTools, string? ParameterSize)
{
    /// <summary>A model is known by its name; lists and screen readers show this.</summary>
    public override string ToString() => Name;
}

/// <summary>One message in a request to the model.</summary>
public sealed record ChatTurn(
    ChatRole Role,
    string Content,
    string? ToolName = null,
    IReadOnlyList<ToolCall>? ToolCalls = null);

/// <param name="ParametersJson">JSON Schema object describing the tool's arguments.</param>
public sealed record ToolDefinition(string Name, string Description, string ParametersJson);

public sealed record ModelOptions(string Model, double Temperature, int ContextLength, int KeepAliveMinutes);

/// <summary>Something the model produced while streaming a reply.</summary>
public abstract record ModelEvent;

public sealed record ModelText(string Text) : ModelEvent;

public sealed record ModelToolCall(ToolCall Call) : ModelEvent;

/// <param name="HitLengthLimit">True when the reply was cut off at the longest length allowed, not finished by the model.</param>
public sealed record ModelDone(int PromptTokens, int OutputTokens, bool HitLengthLimit = false) : ModelEvent;

public interface IOllamaClient
{
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a model into memory ahead of time, so the first reply doesn't start with a wait of
    /// several seconds while Ollama loads it.
    /// </summary>
    Task PreloadAsync(ModelOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams one reply. Text arrives as <see cref="ModelText"/> pieces; the stream ends with <see cref="ModelDone"/>.
    /// </summary>
    IAsyncEnumerable<ModelEvent> StreamChatAsync(
        ModelOptions options,
        IReadOnlyList<ChatTurn> turns,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default);
}
