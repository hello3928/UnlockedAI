using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Data;

public sealed class MessageRepository(Database database) : RepositoryBase(database)
{
    /// <summary>
    /// Saves a message at the end of the chat, with its attachments, and marks the chat as just used.
    /// All of it commits together or not at all.
    /// </summary>
    public Task<ChatMessage> AppendAsync(
        long conversationId,
        NewMessage message,
        CancellationToken cancellationToken = default) =>
        Database.RunAsync(
            connection =>
            {
                using var transaction = connection.BeginTransaction();
                var now = NowUnixMs();

                int seq;
                using (var next = Command(
                    connection,
                    "SELECT COALESCE(MAX(seq), -1) + 1 FROM messages WHERE conversation_id = $conversation",
                    ("$conversation", conversationId)))
                {
                    seq = Convert.ToInt32(next.ExecuteScalar(), CultureInfo.InvariantCulture);
                }

                long id;
                using (var insert = Command(
                    connection,
                    """
                    INSERT INTO messages (conversation_id, seq, role, content, tool_name, tool_calls_json, created_at)
                    VALUES ($conversation, $seq, $role, $content, $tool, $calls, $now)
                    RETURNING id
                    """,
                    ("$conversation", conversationId),
                    ("$seq", seq),
                    ("$role", message.Role.ToWire()),
                    ("$content", message.Content),
                    ("$tool", message.ToolName),
                    ("$calls", SerializeToolCalls(message.ToolCalls)),
                    ("$now", now)))
                {
                    id = (long)insert.ExecuteScalar()!;
                }

                var attachments = InsertAttachments(connection, id, message.Attachments);

                using (var touch = Command(
                    connection,
                    "UPDATE conversations SET updated_at = $now WHERE id = $conversation",
                    ("$now", now),
                    ("$conversation", conversationId)))
                {
                    touch.ExecuteNonQuery();
                }

                transaction.Commit();

                return new ChatMessage(
                    id,
                    conversationId,
                    seq,
                    message.Role,
                    message.Content,
                    message.ToolName,
                    message.ToolCalls,
                    attachments,
                    FromUnixMs(now));
            },
            cancellationToken);

    /// <summary>All messages of one chat in order. Attachment text is not loaded.</summary>
    public Task<List<ChatMessage>> ListAsync(long conversationId, CancellationToken cancellationToken = default) =>
        Database.RunAsync(
            connection =>
            {
                var attachments = ReadAttachments(connection, conversationId);

                using var command = Command(
                    connection,
                    """
                    SELECT id, seq, role, content, tool_name, tool_calls_json, created_at
                    FROM messages
                    WHERE conversation_id = $conversation
                    ORDER BY seq
                    """,
                    ("$conversation", conversationId));
                using var reader = command.ExecuteReader();

                var messages = new List<ChatMessage>();
                while (reader.Read())
                {
                    var id = reader.GetInt64(0);
                    messages.Add(new ChatMessage(
                        id,
                        conversationId,
                        reader.GetInt32(1),
                        ChatRoleExtensions.ParseWire(reader.GetString(2)),
                        reader.GetString(3),
                        reader.IsDBNull(4) ? null : reader.GetString(4),
                        reader.IsDBNull(5) ? [] : DeserializeToolCalls(reader.GetString(5)),
                        attachments.TryGetValue(id, out var found) ? found : [],
                        FromUnixMs(reader.GetInt64(6))));
                }

                return messages;
            },
            cancellationToken);

    private static List<Attachment> InsertAttachments(
        SqliteConnection connection,
        long messageId,
        IReadOnlyList<NewAttachment> attachments)
    {
        var saved = new List<Attachment>(attachments.Count);
        foreach (var attachment in attachments)
        {
            using var insert = Command(
                connection,
                """
                INSERT INTO attachments (message_id, file_name, kind, size_bytes, truncated, extracted_text)
                VALUES ($message, $name, $kind, $size, $truncated, $text)
                RETURNING id
                """,
                ("$message", messageId),
                ("$name", attachment.FileName),
                ("$kind", attachment.Kind),
                ("$size", attachment.SizeBytes),
                ("$truncated", attachment.Truncated),
                ("$text", attachment.Text));

            saved.Add(new Attachment(
                (long)insert.ExecuteScalar()!,
                attachment.FileName,
                attachment.Kind,
                attachment.SizeBytes,
                attachment.Truncated));
        }

        return saved;
    }

    private static Dictionary<long, List<Attachment>> ReadAttachments(SqliteConnection connection, long conversationId)
    {
        using var command = Command(
            connection,
            """
            SELECT a.message_id, a.id, a.file_name, a.kind, a.size_bytes, a.truncated
            FROM attachments a
            JOIN messages m ON m.id = a.message_id
            WHERE m.conversation_id = $conversation
            ORDER BY a.id
            """,
            ("$conversation", conversationId));
        using var reader = command.ExecuteReader();

        var byMessage = new Dictionary<long, List<Attachment>>();
        while (reader.Read())
        {
            var messageId = reader.GetInt64(0);
            if (!byMessage.TryGetValue(messageId, out var list))
            {
                byMessage[messageId] = list = [];
            }

            list.Add(new Attachment(
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetBoolean(5)));
        }

        return byMessage;
    }

    private static string? SerializeToolCalls(IReadOnlyList<ToolCall> toolCalls) =>
        toolCalls.Count == 0
            ? null
            : JsonSerializer.Serialize(toolCalls, StorageJsonContext.Default.IReadOnlyListToolCall);

    private static IReadOnlyList<ToolCall> DeserializeToolCalls(string json) =>
        JsonSerializer.Deserialize(json, StorageJsonContext.Default.IReadOnlyListToolCall) ?? [];
}
