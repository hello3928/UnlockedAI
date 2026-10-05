namespace UnlockedAI.Core.Models;

/// <summary>A stored message. Attachments carry metadata only; their text stays in the database.</summary>
/// <param name="ToolName">Set on <see cref="ChatRole.Tool"/> messages: the tool this result came from.</param>
/// <param name="ToolCalls">Set on assistant messages that asked for tools to be run.</param>
public sealed record ChatMessage(
    long Id,
    long ConversationId,
    int Seq,
    ChatRole Role,
    string Content,
    string? ToolName,
    IReadOnlyList<ToolCall> ToolCalls,
    IReadOnlyList<Attachment> Attachments,
    DateTimeOffset CreatedAt);

/// <param name="ArgumentsJson">The arguments object exactly as the model produced it.</param>
public sealed record ToolCall(string Name, string ArgumentsJson);

/// <summary>A message about to be saved. The repository assigns its id and position.</summary>
public sealed record NewMessage(ChatRole Role, string Content)
{
    public string? ToolName { get; init; }
    public IReadOnlyList<ToolCall> ToolCalls { get; init; } = [];
    public IReadOnlyList<NewAttachment> Attachments { get; init; } = [];
}
