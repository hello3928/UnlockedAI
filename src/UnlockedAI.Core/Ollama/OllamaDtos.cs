using System.Text.Json;

namespace UnlockedAI.Core.Ollama;

// Wire shapes for Ollama's HTTP API. Property names are converted to snake_case by OllamaJsonContext.

internal sealed class ChatRequestDto
{
    public required string Model { get; init; }
    public required List<MessageDto> Messages { get; init; }
    public bool Stream { get; init; } = true;
    public List<ToolDto>? Tools { get; init; }
    public OptionsDto? Options { get; init; }
    public string? KeepAlive { get; init; }
}

internal sealed class MessageDto
{
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
    public List<ToolCallDto>? ToolCalls { get; init; }
    public string? ToolName { get; init; }
}

internal sealed class ToolCallDto
{
    public FunctionCallDto? Function { get; init; }
}

internal sealed class FunctionCallDto
{
    public string Name { get; init; } = "";
    public JsonElement Arguments { get; init; }
}

internal sealed class ToolDto
{
    public string Type { get; init; } = "function";
    public required FunctionDto Function { get; init; }
}

internal sealed class FunctionDto
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonElement Parameters { get; init; }
}

internal sealed class OptionsDto
{
    public double Temperature { get; init; }
    public int NumCtx { get; init; }
}

internal sealed class ChatChunkDto
{
    public MessageDto? Message { get; init; }
    public bool Done { get; init; }
    public string? Error { get; init; }
    public int? PromptEvalCount { get; init; }
    public int? EvalCount { get; init; }
}

internal sealed class TagsDto
{
    public List<TagModelDto>? Models { get; init; }
}

internal sealed class TagModelDto
{
    public string Name { get; init; } = "";
    public long Size { get; init; }
    public TagDetailsDto? Details { get; init; }
    public List<string>? Capabilities { get; init; }
}

internal sealed class TagDetailsDto
{
    public string? ParameterSize { get; init; }
}

internal sealed class ErrorDto
{
    public string? Error { get; init; }
}
