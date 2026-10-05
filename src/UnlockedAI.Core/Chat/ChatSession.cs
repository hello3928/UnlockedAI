using System.Runtime.CompilerServices;
using System.Text;
using UnlockedAI.Core.Data;
using UnlockedAI.Core.Errors;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Ollama;

namespace UnlockedAI.Core.Chat;

/// <summary>
/// Produces the model's reply for a chat whose latest message is already saved, and saves what it produces.
/// Does its work off the caller's thread; consume the events from wherever suits.
/// </summary>
public sealed class ChatSession(
    IOllamaClient ollama,
    MessageRepository messages,
    AttachmentRepository attachments,
    SettingsService settings)
{
    public async IAsyncEnumerable<ChatEvent> RunAsync(
        Conversation conversation,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var current = settings.Current;
        var options = new ModelOptions(
            conversation.Model,
            current.Temperature,
            current.ContextLength,
            current.KeepAliveMinutes);

        var turns = await LoadTurnsAsync(conversation.Id, current.SystemPrompt, cancellationToken).ConfigureAwait(false);

        yield return new AssistantStarted();

        var text = new StringBuilder();
        var finished = false;
        try
        {
            await foreach (var modelEvent in ollama
                .StreamChatAsync(options, turns, tools: [], cancellationToken)
                .ConfigureAwait(false))
            {
                if (modelEvent is ModelText delta)
                {
                    text.Append(delta.Text);
                    yield return new AssistantDelta(delta.Text);
                }
            }

            finished = true;
        }
        finally
        {
            // Stopped or failed part-way: keep what the user already saw, so the saved chat matches the screen.
            if (!finished && text.Length > 0)
            {
                await SaveAssistantAsync(conversation.Id, text.ToString(), CancellationToken.None).ConfigureAwait(false);
            }
        }

        if (text.Length == 0)
        {
            throw new AppException(new AppError(
                AppErrorKind.Unknown,
                "Empty reply",
                "The model returned nothing. Try again.",
                IsRetryable: true));
        }

        var saved = await SaveAssistantAsync(conversation.Id, text.ToString(), cancellationToken).ConfigureAwait(false);
        yield return new AssistantCompleted(saved);
    }

    private async Task<List<ChatTurn>> LoadTurnsAsync(long conversationId, string systemPrompt, CancellationToken cancellationToken)
    {
        var history = await messages.ListAsync(conversationId, cancellationToken).ConfigureAwait(false);
        var files = await attachments.ListTextAsync(conversationId, cancellationToken).ConfigureAwait(false);
        return ContextBuilder.Build(systemPrompt, history, files);
    }

    private Task<ChatMessage> SaveAssistantAsync(long conversationId, string content, CancellationToken cancellationToken) =>
        messages.AppendAsync(conversationId, new NewMessage(ChatRole.Assistant, content), cancellationToken);
}
