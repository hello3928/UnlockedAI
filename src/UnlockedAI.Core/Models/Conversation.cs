namespace UnlockedAI.Core.Models;

public sealed record Conversation(
    long Id,
    string Title,
    string Model,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>What the sidebar needs for one row. Nothing else is loaded until a chat is opened.</summary>
public sealed record ConversationSummary(long Id, string Title, DateTimeOffset UpdatedAt);
