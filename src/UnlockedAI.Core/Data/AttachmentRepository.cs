using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Data;

public sealed class AttachmentRepository(Database database) : RepositoryBase(database)
{
    /// <summary>
    /// Extracted text for every attachment in a chat. Call this only while building a request
    /// to the model and let the result go afterwards; it can be large.
    /// </summary>
    public Task<List<AttachmentText>> ListTextAsync(long conversationId, CancellationToken cancellationToken = default) =>
        Database.RunAsync(
            connection =>
            {
                using var command = Command(
                    connection,
                    """
                    SELECT a.message_id, a.file_name, a.truncated, a.extracted_text
                    FROM attachments a
                    JOIN messages m ON m.id = a.message_id
                    WHERE m.conversation_id = $conversation
                    ORDER BY a.id
                    """,
                    ("$conversation", conversationId));
                using var reader = command.ExecuteReader();

                var texts = new List<AttachmentText>();
                while (reader.Read())
                {
                    texts.Add(new AttachmentText(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.GetBoolean(2),
                        reader.GetString(3)));
                }

                return texts;
            },
            cancellationToken);
}
