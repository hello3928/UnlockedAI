using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Chat;

/// <summary>A step in producing a reply, reported by <see cref="ChatSession"/> in the order it happens.</summary>
public abstract record ChatEvent;

/// <summary>The model is about to write a new assistant message.</summary>
public sealed record AssistantStarted : ChatEvent;

/// <summary>More text for the assistant message in progress.</summary>
public sealed record AssistantDelta(string Text) : ChatEvent;

/// <summary>The assistant message is finished and saved.</summary>
public sealed record AssistantCompleted(ChatMessage Message) : ChatEvent;
