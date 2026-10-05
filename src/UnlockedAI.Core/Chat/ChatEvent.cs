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

/// <summary>The model asked for a tool.</summary>
/// <param name="CallId">Ties this to the later events for the same call, and to the approval request.</param>
/// <param name="Title">The tool's name as shown to the user.</param>
/// <param name="Summary">What this call will do: the command, the address, the path.</param>
/// <param name="NeedsApproval">True when the call is now waiting for the user to allow or deny it.</param>
public sealed record ToolStarted(int CallId, string ToolName, string Title, string Summary, bool NeedsApproval) : ChatEvent;

/// <summary>The user allowed a call that was waiting, and it is now running.</summary>
public sealed record ToolRunning(int CallId) : ChatEvent;

/// <summary>The call ended. <paramref name="Output"/> is what the model was given back.</summary>
public sealed record ToolFinished(int CallId, ToolOutcome Outcome, string Output) : ChatEvent;
