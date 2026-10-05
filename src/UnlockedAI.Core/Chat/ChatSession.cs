using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Errors;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Ollama;
using UnlockedAI.Core.Tools;

namespace UnlockedAI.Core.Chat;

/// <summary>
/// Produces the model's reply for a chat whose latest message is already saved, and saves what it produces.
/// When the model asks for tools, it runs them (waiting for the user's approval where needed), hands the
/// results back, and lets the model continue, until the model answers without asking for more.
/// Does its work off the caller's thread; consume the events from wherever suits.
/// </summary>
public sealed class ChatSession(
    IOllamaClient ollama,
    MessageRepository messages,
    AttachmentRepository attachments,
    SettingsService settings,
    ToolRegistry tools,
    IApprovalGate approvals,
    TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>How many times in a row the model may use tools before it has to answer. Stops a model stuck in a loop.</summary>
    public const int MaxRounds = 10;

    private const string StoppedMessage = "Error: Stopped before this tool finished.";

    // "Allow for this chat" choices. Kept in memory only, so they end when the app closes.
    private readonly HashSet<(long ConversationId, string ToolName)> _allowedForChat = [];
    private readonly Lock _allowedGate = new();

    private int _lastCallId;

    /// <param name="useTools">False for models that can't call tools; they get a plain conversation.</param>
    public async IAsyncEnumerable<ChatEvent> RunAsync(
        Conversation conversation,
        bool useTools,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var current = settings.Current;
        var options = new ModelOptions(
            conversation.Model,
            current.Temperature,
            current.ContextLength,
            current.KeepAliveMinutes);

        IReadOnlyList<ToolDefinition> offered = useTools ? tools.DefinitionsFor(current) : [];
        var turns = await LoadTurnsAsync(conversation.Id, current, offered.Count > 0, cancellationToken)
            .ConfigureAwait(false);

        for (var round = 0; round < MaxRounds; round++)
        {
            yield return new AssistantStarted();

            var text = new StringBuilder();
            var calls = new List<ToolCall>();
            var finished = false;
            var hitLengthLimit = false;
            try
            {
                await foreach (var modelEvent in ollama
                    .StreamChatAsync(options, turns, offered, cancellationToken)
                    .ConfigureAwait(false))
                {
                    switch (modelEvent)
                    {
                        case ModelText delta:
                            text.Append(delta.Text);
                            yield return new AssistantDelta(delta.Text);
                            break;

                        case ModelToolCall requested:
                            calls.Add(requested.Call);
                            break;

                        case ModelDone done:
                            hitLengthLimit = done.HitLengthLimit;
                            break;
                    }
                }

                finished = true;
            }
            finally
            {
                // Stopped or failed part-way: keep what the user already saw, so the saved chat matches the screen.
                if (!finished && text.Length > 0)
                {
                    await messages
                        .AppendAsync(conversation.Id, new NewMessage(ChatRole.Assistant, text.ToString()), CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }

            // A tool call the model wrote out as text is run, not shown as a line of JSON.
            if (calls.Count == 0
                && offered.Count > 0
                && TextToolCall.TryParse(text.ToString(), offered.Select(tool => tool.Name), out var written))
            {
                calls.Add(written);
                text.Clear();
            }

            if (text.Length == 0 && calls.Count == 0)
            {
                // Nothing visible and the length limit reached means the model was writing a tool call
                // that never ended; Ollama holds a tool call back until it is complete.
                throw new AppException(hitLengthLimit
                    ? new AppError(
                        AppErrorKind.ToolFailed,
                        "The model got stuck",
                        "It started a tool call and never finished it, so it was stopped. Try again, or word the request differently.",
                        IsRetryable: true)
                    : new AppError(
                        AppErrorKind.Unknown,
                        "Empty reply",
                        "The model returned nothing. Try again.",
                        IsRetryable: true));
            }

            var saved = await messages
                .AppendAsync(
                    conversation.Id,
                    new NewMessage(ChatRole.Assistant, text.ToString()) { ToolCalls = calls },
                    cancellationToken)
                .ConfigureAwait(false);
            yield return new AssistantCompleted(saved);

            if (calls.Count == 0)
            {
                yield break;
            }

            turns.Add(new ChatTurn(ChatRole.Assistant, saved.Content, ToolCalls: calls));

            var answered = 0;
            try
            {
                foreach (var call in calls)
                {
                    await foreach (var toolEvent in RunToolAsync(conversation.Id, call, current, turns, cancellationToken)
                        .ConfigureAwait(false))
                    {
                        yield return toolEvent;
                    }

                    answered++;
                }
            }
            finally
            {
                // Every tool call the model made needs an answer on record, or the saved chat can't be continued.
                for (var index = answered; index < calls.Count; index++)
                {
                    await messages
                        .AppendAsync(
                            conversation.Id,
                            new NewMessage(ChatRole.Tool, StoppedMessage) { ToolName = calls[index].Name, ToolOutcome = ToolOutcome.Failed },
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
        }

        throw new AppException(new AppError(
            AppErrorKind.ToolFailed,
            "Too many tool steps",
            $"The model used tools {MaxRounds} times in a row without finishing. Send a message if you want it to carry on."));
    }

    private async IAsyncEnumerable<ChatEvent> RunToolAsync(
        long conversationId,
        ToolCall call,
        AppSettings current,
        List<ChatTurn> turns,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var callId = Interlocked.Increment(ref _lastCallId);
        using var arguments = ParseArguments(call.ArgumentsJson);

        ToolResult result;
        if (tools.Find(call.Name) is not { } tool || current.DisabledTools.Contains(call.Name))
        {
            yield return new ToolStarted(callId, call.Name, call.Name, call.ArgumentsJson, NeedsApproval: false);
            result = ToolResult.Failure($"There is no tool named \"{call.Name}\" available.");
        }
        else
        {
            var needsApproval = NeedsApproval(conversationId, tool, arguments.RootElement, current);
            yield return new ToolStarted(callId, tool.Name, tool.Title, tool.Describe(arguments.RootElement), needsApproval);

            var allowed = true;
            if (needsApproval)
            {
                var decision = await approvals.RequestAsync(callId, cancellationToken).ConfigureAwait(false);
                allowed = decision != ApprovalDecision.Deny;

                if (decision == ApprovalDecision.AllowForChat)
                {
                    lock (_allowedGate)
                    {
                        _allowedForChat.Add((conversationId, tool.Name));
                    }
                }

                if (allowed)
                {
                    yield return new ToolRunning(callId);
                }
            }

            result = allowed
                ? await tool.RunAsync(arguments.RootElement, cancellationToken).ConfigureAwait(false)
                : ToolResult.Denied();
        }

        await messages
            .AppendAsync(
                conversationId,
                new NewMessage(ChatRole.Tool, result.Output) { ToolName = call.Name, ToolOutcome = result.Outcome },
                cancellationToken)
            .ConfigureAwait(false);
        turns.Add(new ChatTurn(ChatRole.Tool, result.Output, ToolName: call.Name));

        yield return new ToolFinished(callId, result.Outcome, result.Output);
    }

    private bool NeedsApproval(long conversationId, ITool tool, JsonElement arguments, AppSettings current)
    {
        if (current.ApprovalMode == ApprovalMode.AllowEverything || tool.RiskFor(arguments) == ToolRisk.ReadOnly)
        {
            return false;
        }

        lock (_allowedGate)
        {
            return !_allowedForChat.Contains((conversationId, tool.Name));
        }
    }

    /// <summary>Models occasionally produce arguments that aren't valid JSON; the tool then sees an empty object and reports what is missing.</summary>
    private static JsonDocument ParseArguments(string json)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}");
        }
    }

    private async Task<List<ChatTurn>> LoadTurnsAsync(
        long conversationId,
        AppSettings current,
        bool withTools,
        CancellationToken cancellationToken)
    {
        var history = await messages.ListAsync(conversationId, cancellationToken).ConfigureAwait(false);
        var files = await attachments.ListTextAsync(conversationId, cancellationToken).ConfigureAwait(false);
        return ContextBuilder.Build(
            current.SystemPrompt,
            history,
            files,
            withTools,
            Situation.Describe(_clock, current, withTools));
    }
}
