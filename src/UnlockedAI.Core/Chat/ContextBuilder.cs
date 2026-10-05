using System.Text;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Ollama;

namespace UnlockedAI.Core.Chat;

/// <summary>Turns stored messages into what is sent to the model: system prompt first, attached files inlined.</summary>
public static class ContextBuilder
{
    public static List<ChatTurn> Build(
        string systemPrompt,
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<AttachmentText> files)
    {
        var turns = new List<ChatTurn>(history.Count + 1);
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            turns.Add(new ChatTurn(ChatRole.System, systemPrompt));
        }

        var filesByMessage = files.ToLookup(file => file.MessageId);
        foreach (var message in history)
        {
            var content = message.Attachments.Count == 0
                ? message.Content
                : WithFiles(message.Content, filesByMessage[message.Id]);

            turns.Add(new ChatTurn(message.Role, content, message.ToolName, message.ToolCalls));
        }

        return turns;
    }

    private static string WithFiles(string content, IEnumerable<AttachmentText> files)
    {
        var builder = new StringBuilder();
        foreach (var file in files)
        {
            builder.Append("[File: ").Append(file.FileName);
            if (file.Truncated)
            {
                builder.Append(" (truncated, only the first part is included)");
            }

            builder.AppendLine("]");
            builder.AppendLine(file.Text);
            builder.Append("[End of file: ").Append(file.FileName).AppendLine("]");
            builder.AppendLine();
        }

        return builder.Append(content).ToString();
    }
}
